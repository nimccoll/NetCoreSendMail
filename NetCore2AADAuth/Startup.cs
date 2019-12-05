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
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using NetCore2AADAuth.Services;
using System;
using System.Threading.Tasks;

namespace NetCore2AADAuth
{
    public class Startup
    {
        private readonly string _graphResourceId = "https://graph.windows.net";

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.AddDataProtection();

            services.AddScoped<ITokenCacheFactory, TokenCacheFactory>();

            services.AddAuthentication(auth =>
            {
                auth.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                auth.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                auth.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
                .AddCookie()
                .AddOpenIdConnect(opts =>
                {
                    Configuration.GetSection("OpenIdConnect").Bind(opts);
                    opts.Events = new OpenIdConnectEvents
                    {
                        OnAuthorizationCodeReceived = async context =>
                        {
                            // Construct token cache
                            IDistributedCache distributedCache = context.HttpContext.RequestServices.GetRequiredService<IDistributedCache>();
                            ITokenCacheFactory cacheFactory = context.HttpContext.RequestServices.GetRequiredService<ITokenCacheFactory>();
                            TokenCache cache = cacheFactory.CreateForUser(context.Principal);

                            // Obtain access token for the current application using the authorization code
                            HttpRequest request = context.HttpContext.Request;
                            string currentUri = UriHelper.BuildAbsolute(request.Scheme, request.Host, request.PathBase, request.Path);
                            ClientCredential credential = new ClientCredential(context.Options.ClientId, context.Options.ClientSecret);
                            AuthenticationContext authContext = new AuthenticationContext(context.Options.Authority, cache);
                            AuthenticationResult result = await authContext.AcquireTokenByAuthorizationCodeAsync(context.ProtocolMessage.Code, new Uri(currentUri), credential, context.Options.ClientId);

                            // Obtain and cache access tokens for additional resources using the access token
                            // from the application as an assertion
                            UserAssertion userAssertion = new UserAssertion(result.AccessToken);
                            AuthenticationResult graphResult = await authContext.AcquireTokenAsync(_graphResourceId, credential, userAssertion);

                            context.HandleCodeRedemption(result.AccessToken, result.IdToken);
                        }
                    };
                });

            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();
            app.UseAuthentication();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
