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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using NetCoreSendMail.Services;
using System;

namespace NetCoreSendMail
{
    public class Startup
    {
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
                        OnAuthorizationCodeReceived = async ctx =>
                        {
                            var request = ctx.HttpContext.Request;
                            var currentUri = UriHelper.BuildAbsolute(request.Scheme, request.Host, request.PathBase, request.Path);
                            var credential = new ClientCredential(ctx.Options.ClientId, ctx.Options.ClientSecret);

                            var distributedCache = ctx.HttpContext.RequestServices.GetRequiredService<IDistributedCache>();
                            string userId = ctx.Principal.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier").Value;

                            //Construct token cache
                            ITokenCacheFactory cacheFactory = ctx.HttpContext.RequestServices.GetRequiredService<ITokenCacheFactory>();
                            TokenCache cache = cacheFactory.CreateForUser(ctx.Principal);

                            var authContext = new AuthenticationContext(ctx.Options.Authority, cache);

                            var result = await authContext.AcquireTokenByAuthorizationCodeAsync(
                                ctx.ProtocolMessage.Code, new Uri(currentUri), credential, ctx.Options.Resource);

                            ctx.HandleCodeRedemption(result.AccessToken, result.IdToken);
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
