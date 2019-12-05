//===============================================================================
// Microsoft Premier Support for Developers
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
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using NetCore2AADAuth.Extensions;
using NetCore2AADAuth.Models;
using NetCore2AADAuth.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace NetCore2AADAuth.Controllers
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
            string accessToken = GetGraphAccessToken();
            if (string.IsNullOrEmpty(accessToken))
            {
                return RedirectToAction("Index");
            }
            else
            {
                _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                HttpResponseMessage graphResult = _client.GetAsync("https://graph.windows.net/me?api-version=1.6").Result;
                dynamic graphData = JsonConvert.DeserializeObject(graphResult.Content.ReadAsStringAsync().Result);
                ViewBag.DisplayName = graphData.displayName.ToString();
                ViewBag.FirstName = graphData.givenName.ToString();
                ViewBag.LastName = graphData.surname.ToString();
                ViewBag.ObjectId = graphData.objectId.ToString();
                ViewBag.Mail = graphData.mail.ToString();
                ViewBag.UserPrincipalName = graphData.userPrincipalName.ToString();

                return View();
            }
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

        public JsonResult People(string searchText)
        {
            List<AutoCompleteResult> results = new List<AutoCompleteResult>();

            string accessToken = GetGraphAccessToken();
            if (string.IsNullOrEmpty(accessToken))
            {
                // throw a 401 exception here
            }
            else
            {
                string tenant = _configuration.GetValue<string>("OpenIdConnect:Authority").Replace("https://login.microsoftonline.com/", "").Replace("/", "");
                _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                HttpResponseMessage graphResult = _client.GetAsync(string.Format("https://graph.windows.net/{0}/users?api-version=1.6&$filter=startswith(surname,'{1}')", tenant, searchText)).Result;
                dynamic graphData = JsonConvert.DeserializeObject(graphResult.Content.ReadAsStringAsync().Result);
                if (graphData != null)
                {
                    foreach (dynamic user in graphData.value)
                    {
                        AutoCompleteResult result = new AutoCompleteResult();
                        result.label = user.displayName;
                        result.value = user.userPrincipalName;
                        results.Add(result);
                    }
                }
            }

            return Json(results);
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
                    "https://graph.windows.net",
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

        public FileResult ServeIndex()
        {
            var file = Path.Combine(Directory.GetCurrentDirectory(),
                                    "index.html");

            return PhysicalFile(file, "text/html");
        }
    }
}
