using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Babou.API.Web.Models;
using Babou.API.Web.Models.Database;
using Babou.API.Web.Services.Interfaces;
using BabouExtensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Azure.Storage.Shared.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Babou.API.Web.Areas.Identity.Pages.Account.Manage
{
    public class UrlShortenerModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<UrlShortenerModel> _logger;
        private readonly IUrlShortenerService _urlShortenerService;
        private readonly AppSettings _appSettings;
        private string _existingDomain;

        public UrlShortenerModel(UserManager<ApplicationUser> userManager, ILogger<UrlShortenerModel> logger,
            IUrlShortenerService urlShortenerService, IOptionsMonitor<AppSettings> appSettings)
        {
            _userManager = userManager;
            _logger = logger;
            _urlShortenerService = urlShortenerService;
            _appSettings = appSettings.CurrentValue;
        }
        [TempData]
        public string ExistingLongUrl { get; set; }

        [TempData]
        public string ExistingDomain
        {
            get => !string.IsNullOrEmpty(_existingDomain) ? _existingDomain : _appSettings.BaseShortenedDefaultUrl;
            set => _existingDomain = value;
        }

        public string Token { get; set; }

        public List<SelectListItem> DomainOptions { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        public List<ShortenedUrl> ShortenedUrls { get; set; }

        [BindProperty]
        public ShortenedUrl ShortenedUrl { get; set; }

        private async Task LoadAsync(ApplicationUser user, bool userIsAdmin)
        {
            DomainOptions = _appSettings.BaseShortenedUrls
                .WhereIf(!userIsAdmin, x => x != Helpers.Constants.ShortDomainUrls.AjtGo)
                .OrderBy(x => x)
                .Select(x =>
                    new SelectListItem
                    {
                        Value = x,
                        Text = x
                    }).ToList();

            ShortenedUrls = await _urlShortenerService.GetShortenedUrlsByUserId(user.Id);

            Token = await _urlShortenerService.GetToken(ExistingDomain);

            ShortenedUrl = new ShortenedUrl()
            {
                Token = Token,
                LongUrl = ExistingLongUrl,
                ShortUrl = _urlShortenerService.GetShortUrl(ExistingDomain, Token),
                Domain = ExistingDomain,
                CreatedBy = user.ApiAuthKey
            };
        }

        public async Task<IActionResult> OnGet()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Error: Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var userIsAdmin = await _userManager.IsInRoleAsync(user, Helpers.Constants.Roles.Admin);

            await LoadAsync(user, userIsAdmin);
            return Page();
        }

        public async Task<IActionResult> OnPost()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Error: Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            if (Token != ShortenedUrl.Token)
            {
                //user wants their own token
                try
                {
                    var userTokenAvailable = await _urlShortenerService.CheckIfTokenIsAvailable(ShortenedUrl.Token, ShortenedUrl.Domain);
                    if (userTokenAvailable)
                        Token = ShortenedUrl.Token;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "UrlShortenerModel: Error while trying to create custom token {Token} by {UserId}.", Token, user.Id);
                    StatusMessage = $"Error: {ex.Message}";
                    return Page();
                }
            }

            if (ModelState.IsValid)
            {
                var shortenedUrl = await _urlShortenerService.CreateByUserId(user.Id, ShortenedUrl.LongUrl, ShortenedUrl.Domain, Token);
                _logger.LogInformation("UrlShortenerModel: New Shortened Url Created for {LongUrl} as {ShortUrl}. Created by {UserId}", shortenedUrl.LongUrl, shortenedUrl.ShortUrl, user.Id);
                
                StatusMessage = $"The short url {shortenedUrl.ShortUrl} has been created!";
                return RedirectToPage();
            }
            else
            {
                StatusMessage = "Error: Please review the errors below.";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostUpdate(int id)
        {
            try
            {
                var shortenedUrl = await _urlShortenerService.UpdateById(id, ShortenedUrl.LongUrl);
                StatusMessage = $"{shortenedUrl.ShortUrl} has been updated!";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OnPostUpdate: Unable to find ShortenedUrl {Id}", id);
                StatusMessage = $"Error: Something went wrong updating {id}.";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostDelete(int id)
        {
            try
            {
                var shortenedUrl = await _urlShortenerService.DeleteById(id);
                StatusMessage = $"{shortenedUrl.ShortUrl} has been deleted!";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OnPostDelete: Unable to find ShortenedUrl {Id}", id);
                StatusMessage = $"Error: Something went wrong retrieving Shortened Url Token {id}.";
                return Page();
            }
        }

        public IActionResult OnPostRegenerateToken()
        {
            ExistingLongUrl = ShortenedUrl.LongUrl;
            ExistingDomain = ShortenedUrl.Domain;
            return RedirectToPage();
        }
    }
}
