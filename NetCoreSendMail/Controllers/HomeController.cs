//===============================================================================
// Microsoft FastTrack for Azure
// ASP.Net Core 2 Send Mail
//===============================================================================
// Copyright © Microsoft Corporation.  All rights reserved.
// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY
// OF ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT
// LIMITED TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
// FITNESS FOR A PARTICULAR PURPOSE.
//===============================================================================
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using NetCoreSendMail.Extensions;
using NetCoreSendMail.Models;
using NetCoreSendMail.Services;
using Newtonsoft.Json.Linq;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace NetCoreSendMail.Controllers
{
    public class HomeController : Controller
    {
        private static readonly HttpClient _client = new HttpClient();
        private readonly ITokenCacheFactory _tokenCacheFactory;
        private readonly IConfiguration _configuration;

        public HomeController(ITokenCacheFactory tokenCacheFactory, IConfiguration configuration)
        {
            _tokenCacheFactory = tokenCacheFactory;
            _configuration = configuration;
        }

        [Authorize]
        public IActionResult Index()
        {
            return View();
        }

        [Authorize]
        public IActionResult SendMailAsCurrentUser()
        {
            string accessToken = GetGraphAccessToken();

            if (string.IsNullOrEmpty(accessToken))
            {
                return RedirectToAction("Index");
            }
            else
            {
                SendMail(accessToken, false, false);
            }

            return View();
        }

        public IActionResult SendMailAsFunctionalAccount()
        {
            string accessToken = GetGraphAccessTokenForFunctionalAccount();

            if (string.IsNullOrEmpty(accessToken))
            {
                return RedirectToAction("Index");
            }
            else
            {
                SendMail(accessToken, true, false);
            }

            return View();
        }

        public IActionResult SendMailAsApplication()
        {
            string accessToken = GetGraphAccessTokenForApp();
            if (string.IsNullOrEmpty(accessToken))
            {
                return RedirectToAction("Index");
            }
            else
            {
                SendMail(accessToken, false, true);
            }

            return View();
        }

        public IActionResult SendMailViaSendGrid()
        {
            SendGridClient client = new SendGridClient(_configuration.GetValue<string>("SENDGRID_APIKEY"));
            SendGrid.Helpers.Mail.EmailAddress from = new SendGrid.Helpers.Mail.EmailAddress(_configuration.GetValue<string>("MicrosoftGraph:UserName"));
            List<SendGrid.Helpers.Mail.EmailAddress> tos = new List<SendGrid.Helpers.Mail.EmailAddress>
            {
                new SendGrid.Helpers.Mail.EmailAddress(_configuration.GetValue<string>("EmailRecipient"))
            };

            string subject = "Test message from SendGrid";
            string textContent = "Test message from SendGrid";
            bool displayRecipients = false; // set this to true if you want recipients to see each others mail id 
            SendGridMessage msg = MailHelper.CreateSingleEmailToMultipleRecipients(from, tos, subject, textContent, "", displayRecipients);
            Response response = client.SendEmailAsync(msg).Result;

            return View();
        }

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
        
        private string GetGraphAccessToken()
        {
            string accessToken = string.Empty;
            string authority = _configuration.GetValue<string>("OpenIdConnect:Authority");

            TokenCache cache = _tokenCacheFactory.CreateForUser(User);

            AuthenticationContext authContext = new AuthenticationContext(authority, cache);

            // App's credentials may be needed if access tokens need to be refreshed with a refresh token
            string clientId = _configuration.GetValue<string>("OpenIdConnect:ClientId");
            string clientSecret = _configuration.GetValue<string>("OpenIdConnect:ClientSecret");
            ClientCredential credential = new ClientCredential(clientId, clientSecret);
            string userId = User.GetObjectId();

            AuthenticationResult result = null;

            try
            {
                result = authContext.AcquireTokenSilentAsync(
                    _configuration.GetValue<string>("OpenIdConnect:Resource"),
                    credential,
                    new UserIdentifier(userId, UserIdentifierType.UniqueId)).Result;
                accessToken = result.AccessToken;
            }
            catch (Exception ex)
            {
                HttpContext.SignOutAsync().Wait();
            }

            return accessToken;
        }

        private string GetGraphAccessTokenForFunctionalAccount()
        {
            string accessToken = string.Empty;
            string authority = _configuration.GetValue<string>("OpenIdConnect:Authority");
            string tokenEndpoint = string.Format("{0}/oauth2/token", authority);
            string nativeClientId = _configuration.GetValue<string>("MicrosoftGraph:ClientId");
            string userName = _configuration.GetValue<string>("MicrosoftGraph:UserName");
            string password = _configuration.GetValue<string>("MicrosoftGraph:Password"); ;
            string body = string.Format("resource={0}&client_id={1}&grant_type=password&username={2}&password={3}", _configuration.GetValue<string>("OpenIdConnect:Resource"), nativeClientId, userName, password);
            StringContent stringContent = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");

            HttpResponseMessage response = _client.PostAsync(tokenEndpoint, stringContent).Result;
            string result = response.Content.ReadAsStringAsync().Result;

            JObject jobject = JObject.Parse(result);

            accessToken = jobject["access_token"].Value<string>();

            return accessToken;
        }

        private string GetGraphAccessTokenForApp()
        {
            string accessToken = string.Empty;
            string authority = _configuration.GetValue<string>("OpenIdConnect:Authority");
            AuthenticationContext authenticationContext = new AuthenticationContext(authority);
            string clientId = _configuration.GetValue<string>("OpenIdConnect:ClientId");
            string clientSecret = _configuration.GetValue<string>("OpenIdConnect:ClientSecret");
            ClientCredential credential = new ClientCredential(clientId, clientSecret);

            AuthenticationResult result = null;

            try
            {
                result = authenticationContext.AcquireTokenAsync(_configuration.GetValue<string>("OpenIdConnect:Resource"), credential).Result;
                accessToken = result.AccessToken;
            }
            catch (Exception ex)
            {
                string message = ex.Message;
            }

            return accessToken;
        }

        private void SendMail(string accessToken, bool isFunctionalAccount, bool isApplication)
        {
            GraphServiceClient graphClient = new GraphServiceClient(
                new DelegateAuthenticationProvider(
                    async (requestMessage) =>
                    {
                        // Append the access token to the request.
                        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", accessToken);

                        // Get event times in the current time zone.
                        requestMessage.Headers.Add("Prefer", "outlook.timezone=\"" + TimeZoneInfo.Local.Id + "\"");
                    }));

            List<Recipient> recipients = new List<Recipient>();
            recipients.Add(new Recipient
            {
                EmailAddress = new Microsoft.Graph.EmailAddress
                {
                    Address = _configuration.GetValue<string>("EmailRecipient")
                }
            });

            string message = string.Empty;
            if (isFunctionalAccount)
            {
                message = "Test message from Microsoft Graph using Functional Account";
            }
            else if (isApplication)
            {
                message = "Test message from Microsoft Graph using Application Credentials";
            }
            else
            {
                message = "Test message from Microsoft Graph using Current User";
            }

            // Create the message.
            Message email = new Message
            {
                Body = new ItemBody
                {
                    Content = message,
                    ContentType = BodyType.Text,
                },
                Subject = message,
                ToRecipients = recipients
            };

            // Send the message.
            if (isApplication)
            {
                graphClient.Users[_configuration.GetValue<string>("MicrosoftGraph:UserName")].SendMail(email, true).Request().PostAsync().Wait();
            }
            else
            {
                graphClient.Me.SendMail(email, true).Request().PostAsync().Wait();
            }
        }
    }
}
