using System.Collections.Generic;
using Microsoft.AspNetCore.Authentication.WeChat;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>
    /// Configuration options for <see cref="WeChatMiddleware"/>.
    /// </summary>
    public class WeChatOptions : OAuthOptions
    {
        /// <summary>
        /// Initializes a new <see cref="WeChatOptions"/>.
        /// </summary>
        public WeChatOptions()
        {
            AuthenticationScheme = WeChatDefaults.AuthenticationScheme;
            DisplayName = AuthenticationScheme;
            CallbackPath = "https://freshpay.net/Alipay/redirect"; // new PathString("/Alipay/redirect");
            StateAddition = "#wechat_redirect";
            AuthorizationEndpoint = WeChatDefaults.AuthorizationEndpoint;
            TokenEndpoint = WeChatDefaults.TokenEndpoint;
            UserInformationEndpoint = WeChatDefaults.UserInformationEndpoint;
            WeChatScope = InfoScope;
        }

        // WeChat uses a non-standard term for this field.s
        /// <summary>
        /// Gets or sets the WeChat-assigned appId.
        /// </summary>
        public string AppId
        {
            get { return ClientId; }
            set { ClientId = value; }
        }

        // WeChat uses a non-standard term for this field.
        /// <summary>
        /// Gets or sets the WeChat-assigned app secret.
        /// </summary>
        public string AppSecret
        {
            get { return ClientSecret; }
            set { ClientSecret = value; }
        }

        public string StateAddition { get; set; }
        public string WeChatScope { get; set; }
        public string BaseScope = "snsapi_base";
        public string InfoScope = "snsapi_userinfo";
    }
}