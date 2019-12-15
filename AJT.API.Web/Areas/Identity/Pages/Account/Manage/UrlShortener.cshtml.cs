using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AJT.API.Web.Models.Database;
using AJT.API.Web.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace AJT.API.Web.Areas.Identity.Pages.Account.Manage
{
    public class UrlShortenerModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<UrlShortenerModel> _logger;
        private readonly IUrlShortenerService _urlShortenerService;

        public UrlShortenerModel(UserManager<ApplicationUser> userManager, ILogger<UrlShortenerModel> logger,
            IUrlShortenerService urlShortenerService)
        {
            _userManager = userManager;
            _logger = logger;
            _urlShortenerService = urlShortenerService;
        }

        [TempData]
        public string StatusMessage { get; set; }

        public List<ShortenedUrl> ShortenedUrls { get; set; }

        [BindProperty]
        public ShortenedUrl ShortenedUrl { get; set; }

        private async Task LoadAsync(ApplicationUser user)
        {
            ShortenedUrls = await _urlShortenerService.GetShortenedUrlsByUserId(user.Id);
        }

        public async Task<IActionResult> OnGet()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Error: Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            await LoadAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPost()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Error: Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var shortenedUrl = await _urlShortenerService.CreateByUserId(user.Id, ShortenedUrl.LongUrl);
            _logger.LogInformation("UrlShortenerModel: New Shortened Url Created for {LongUrl} as {ShortUrl}", shortenedUrl.LongUrl, shortenedUrl.ShortUrl);

            StatusMessage = $"The short url {shortenedUrl.ShortUrl} has been created!";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdate(string id)
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

        public async Task<IActionResult> OnPostDelete(string id)
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
    }
}
