using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WeChatAuthentication.Models;
using WeChatAuthentication.Models.AccountViewModels;
using WeChatAuthentication.Services;

namespace WeChatAuthentication.Controllers
{
    public class WechatController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public string Redirect()
        {
            return Request.Query["echostr"];
        }

        [HttpGet]
        public IActionResult Success()
        {

            return View();
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}
