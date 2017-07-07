using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Authentication;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Authentication.WeChat
{
    internal class WeChatHandler : OAuthHandler<WeChatOptions>
    {
        public WeChatHandler(HttpClient httpClient)
          : base(httpClient)
        {
        }


        protected override async Task<AuthenticateResult> HandleRemoteAuthenticateAsync()
        {
            AuthenticationProperties properties = null;
            var query = Request.Query;

            var error = query["error"];
            if (!StringValues.IsNullOrEmpty(error))
            {
                var failureMessage = new StringBuilder();
                failureMessage.Append(error);
                var errorDescription = query["error_description"];
                if (!StringValues.IsNullOrEmpty(errorDescription))
                {
                    failureMessage.Append(";Description=").Append(errorDescription);
                }
                var errorUri = query["error_uri"];
                if (!StringValues.IsNullOrEmpty(errorUri))
                {
                    failureMessage.Append(";Uri=").Append(errorUri);
                }

                return AuthenticateResult.Fail(failureMessage.ToString());
            }

            var code = query["code"];
            var state = query["state"];
            var oauthState = query["oauthstate"];

            properties = Options.StateDataFormat.Unprotect(oauthState);

            if (state != Options.StateAddition || properties == null)
            {
                return AuthenticateResult.Fail("The oauth state was missing or invalid.");
            }

            // OAuth2 10.12 CSRF
            if (!ValidateCorrelationId(properties))
            {
                return AuthenticateResult.Fail("Correlation failed.");
            }

            if (StringValues.IsNullOrEmpty(code))
            {
                return AuthenticateResult.Fail("Code was not found.");
            }

            //获取tokens
            var tokens = await ExchangeCodeAsync(code, BuildRedirectUri(Options.CallbackPath));

            var identity = new ClaimsIdentity(Options.ClaimsIssuer);

            AuthenticationTicket ticket = null;

            if (Options.WeChatScope == Options.InfoScope)
            {
                //获取用户信息
                ticket = await CreateTicketAsync(identity, properties, tokens);
            }
            else
            {
                //不获取信息，只使用openid
                identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, tokens.TokenType, ClaimValueTypes.String, Options.ClaimsIssuer));
                ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), properties, Options.AuthenticationScheme);
            }

            if (ticket != null)
            {
                return AuthenticateResult.Success(ticket);
            }
            else
            {
                return AuthenticateResult.Fail("Failed to retrieve user information from remote server.");
            }
        }


        /// <summary>
        /// OAuth第一步,获取code
        /// </summary>
        /// <param name="properties"></param>
        /// <param name="redirectUri"></param>
        /// <returns></returns>
        protected override string BuildChallengeUrl(AuthenticationProperties properties, string redirectUri)
        {
            //加密OAuth状态
            var oauthstate = Options.StateDataFormat.Protect(properties);

            //
            redirectUri = $"{redirectUri}?{nameof(oauthstate)}={oauthstate}";

            var queryBuilder = new QueryBuilder()
      {
        { "appid", Options.ClientId },
        { "redirect_uri", redirectUri },
        { "response_type", "code" },
        { "scope", Options.WeChatScope },
        { "state", Options.StateAddition },
      };
            return Options.AuthorizationEndpoint + queryBuilder.ToString();
        }



        /// <summary>
        /// OAuth第二步,获取token
        /// </summary>
        /// <param name="code"></param>
        /// <param name="redirectUri"></param>
        /// <returns></returns>
        protected override async Task<OAuthTokenResponse> ExchangeCodeAsync(string code, string redirectUri)
        {
            var tokenRequestParameters = new Dictionary<string, string>()
      {
        { "appid", Options.ClientId },
        { "secret", Options.ClientSecret },
        { "code", code },
        { "grant_type", "authorization_code" },
      };

            var requestContent = new FormUrlEncodedContent(tokenRequestParameters);

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, Options.TokenEndpoint);
            requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            requestMessage.Content = requestContent;
            var response = await Backchannel.SendAsync(requestMessage, Context.RequestAborted);
            if (response.IsSuccessStatusCode)
            {
                var payload = JObject.Parse(await response.Content.ReadAsStringAsync());

                string ErrCode = payload.Value<string>("errcode");
                string ErrMsg = payload.Value<string>("errmsg");

                if (!string.IsNullOrEmpty(ErrCode) | !string.IsNullOrEmpty(ErrMsg))
                {
                    return OAuthTokenResponse.Failed(new Exception($"ErrCode:{ErrCode},ErrMsg:{ErrMsg}"));
                }

                var tokens = OAuthTokenResponse.Success(payload);

                //借用TokenType属性保存openid
                tokens.TokenType = payload.Value<string>("openid");

                return tokens;
            }
            else
            {
                var error = "OAuth token endpoint failure";
                return OAuthTokenResponse.Failed(new Exception(error));
            }
        }

        /// <summary>
        /// OAuth第四步，获取用户信息
        /// </summary>
        /// <param name="identity"></param>
        /// <param name="properties"></param>
        /// <param name="tokens"></param>
        /// <returns></returns>
        protected override async Task<AuthenticationTicket> CreateTicketAsync(ClaimsIdentity identity, AuthenticationProperties properties, OAuthTokenResponse tokens)
        {
            var queryBuilder = new QueryBuilder()
      {
        { "access_token", tokens.AccessToken },
        { "openid", tokens.TokenType },//在第二步中，openid被存入TokenType属性
        { "lang", "zh_CN" }
      };

            var infoRequest = Options.UserInformationEndpoint + queryBuilder.ToString();

            var response = await Backchannel.GetAsync(infoRequest, Context.RequestAborted);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to retrieve WeChat user information ({response.StatusCode}) Please check if the authentication information is correct and the corresponding WeChat Graph API is enabled.");
            }

            var user = JObject.Parse(await response.Content.ReadAsStringAsync());
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), properties, Options.AuthenticationScheme);
            var context = new OAuthCreatingTicketContext(ticket, Context, Options, Backchannel, tokens, user);

            var identifier = user.Value<string>("openid");
            if (!string.IsNullOrEmpty(identifier))
            {
                identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, identifier, ClaimValueTypes.String, Options.ClaimsIssuer));
            }

            var nickname = user.Value<string>("nickname");
            if (!string.IsNullOrEmpty(nickname))
            {
                identity.AddClaim(new Claim(ClaimTypes.Name, nickname, ClaimValueTypes.String, Options.ClaimsIssuer));
            }

            var sex = user.Value<string>("sex");
            if (!string.IsNullOrEmpty(sex))
            {
                identity.AddClaim(new Claim("urn:WeChat:sex", sex, ClaimValueTypes.String, Options.ClaimsIssuer));
            }

            var country = user.Value<string>("country");
            if (!string.IsNullOrEmpty(country))
            {
                identity.AddClaim(new Claim(ClaimTypes.Country, country, ClaimValueTypes.String, Options.ClaimsIssuer));
            }

            var province = user.Value<string>("province");
            if (!string.IsNullOrEmpty(province))
            {
                identity.AddClaim(new Claim(ClaimTypes.StateOrProvince, province, ClaimValueTypes.String, Options.ClaimsIssuer));
            }

            var city = user.Value<string>("city");
            if (!string.IsNullOrEmpty(city))
            {
                identity.AddClaim(new Claim("urn:WeChat:city", city, ClaimValueTypes.String, Options.ClaimsIssuer));
            }

            var headimgurl = user.Value<string>("headimgurl");
            if (!string.IsNullOrEmpty(headimgurl))
            {
                identity.AddClaim(new Claim("urn:WeChat:headimgurl", headimgurl, ClaimValueTypes.String, Options.ClaimsIssuer));
            }

            var unionid = user.Value<string>("unionid");
            if (!string.IsNullOrEmpty(unionid))
            {
                identity.AddClaim(new Claim("urn:WeChat:unionid", unionid, ClaimValueTypes.String, Options.ClaimsIssuer));
            }

            await Options.Events.CreatingTicket(context);
            return context.Ticket;
        }
    }
}