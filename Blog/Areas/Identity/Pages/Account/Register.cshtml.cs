﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MVCBlog.Models;
using MVCBlog.Services;

namespace MVCBlog.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<BlogUser> _signInManager;
        private readonly UserManager<BlogUser> _userManager;
        private readonly ILogger<RegisterModel> _logger;
        //private readonly IEmailSender _emailSender;
        private readonly IBlogEmailSender _emailSender;
        private readonly IImageService _imageService;
        private readonly IConfiguration _configuration;

        public RegisterModel(
            UserManager<BlogUser> userManager,
            SignInManager<BlogUser> signInManager,
            ILogger<RegisterModel> logger,
            IBlogEmailSender emailSender,
            IImageService imageService,
            IConfiguration configuration )
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _imageService = imageService;
            _configuration = configuration;
        }

        [BindProperty] // Can referer to property from page
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public class InputModel
        {
            [StringLength(50, ErrorMessage = "The {0} must be at least {2} and at most {1} characters.", MinimumLength = 2)]
            [Display(Name = "Display Name")]
            public string DisplayName { get; set; }

            [Required]
            [StringLength(50, ErrorMessage = "The {0} must be at least {2} and at most {1} characters.", MinimumLength = 2)]
            [Display(Name = "First Name")]
            public string FirstName { get; set; }

            [Required]
            [StringLength(75, ErrorMessage = "The {0} must be at least {2} and at most {1} characters.", MinimumLength = 2)]
            [Display(Name = "Last Name")]
            public string LastName { get; set; }

            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Display(Name = "Custom Image")]
            public IFormFile ImageFile { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            if (ModelState.IsValid)
            {
                if(String.IsNullOrEmpty(Input.DisplayName))
                {
                    Input.DisplayName = $"{Input.FirstName} {Input.LastName}";
                }

                var user = new BlogUser {
                    FirstName = Input.FirstName,
                    LastName = Input.LastName,
                    DisplayName = Input.DisplayName,
                    UserName = Input.Email,
                    Email = Input.Email,
                    ImageData = (await _imageService.EncodeImageAsync(Input.ImageFile)) ??
                                 await _imageService.EncodeImageAsync(_configuration["DefaultUserImage"]),
                    ContentType = Input.ImageFile == null ? Path.GetExtension(_configuration["DefaultUserImage"]) : _imageService.ContentType(Input.ImageFile)
                };

                var result = await _userManager.CreateAsync(user, Input.Password);
                if (result.Succeeded)
                {
                    _logger.LogInformation("User created a new account with password.");

                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = user.Id, code = code, returnUrl = returnUrl },
                        protocol: Request.Scheme);

                    await _emailSender.SendEmailAsync(Input.Email, "Confirm your email",
                        $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

                    if (_userManager.Options.SignIn.RequireConfirmedAccount)
                    {
                        return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl = returnUrl });
                    }
                    else
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        return LocalRedirect(returnUrl);
                    }
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }
    }
}
