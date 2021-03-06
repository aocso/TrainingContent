﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Configuration;
using System.Web.Mvc;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.Graph;
using GraphUsersGroups.TokenStorage;
using GraphUsersGroups.Auth;
using GraphUsersGroups.Models;

namespace GraphUsersGroups.Controllers
{
    public class UserSearchController : Controller
    {
        public static string appId = ConfigurationManager.AppSettings["ida:AppId"];
        public static string appSecret = ConfigurationManager.AppSettings["ida:AppSecret"];
        public static string aadInstance = ConfigurationManager.AppSettings["ida:AADInstance"];

        // GET: UserSearch
        public ActionResult Index(string groupId)
        {
            // Need to pass the originating groupId to the view
            GroupModel.groupId = groupId;

            // Return an empty list of people when the page first loads
            List<User> people = new List<User>();
            return View(people);
        }

        [HttpPost]
        public async Task<ActionResult> Index(FormCollection fc, string searchString)
        {
            // Search for users with name or mail that includes searchString.
            var client = GetGraphServiceClient();

            List<User> people = new List<User>();

            // Graph query for users, filtering by displayName, givenName, surname, UPN, mail, and mailNickname.
            // Only query for displayName, userPrincipalName, id of matching users through select.
            try
            {
                var result = await client.Users.Request().Top(7).Filter("startswith(displayName,'" + searchString +
                "') or startswith(givenName,'" + searchString +
                "') or startswith(surname,'" + searchString +
                "') or startswith(userPrincipalName,'" + searchString +
                "') or startswith(mail,'" + searchString +
                "') or startswith(mailNickname,'" + searchString + "')").Select("displayName,userPrincipalName,id").GetAsync();

                // Add users to the list and return to the view.
                foreach (User u in result)
                {
                    people.Add(u);
                }
            }
            catch (Exception)
            {
                return View("Error");
            }
            return View(people);
        }

      

        private GraphServiceClient GetGraphServiceClient()
        {
            string userObjId = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier").Value;
            string tenantID = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid").Value;
            string authority = "common";
            SessionTokenCache tokenCache = new SessionTokenCache(userObjId, HttpContext);

            // Create an authHelper using the the app Id and secret and the token cache.
            AuthHelper authHelper = new AuthHelper(
                authority,
                appId,
                appSecret,
                tokenCache);

            // Request an accessToken and provide the original redirect URL from sign-in.
            GraphServiceClient client = new GraphServiceClient(new DelegateAuthenticationProvider(async (request) =>
            {
                string accessToken = await authHelper.GetUserAccessToken(Url.Action("Index", "Home", null, Request.Url.Scheme));
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + accessToken);
            }));

            return client;
        }
        public async Task<ActionResult> ShowProfile(string userId)
        {
            // Show the profile of a user after a user is clicked from the search.
            var client = GetGraphServiceClient();
            Profile profile = new Profile();

            try
            {
                // Graph query for user details by userId.
                profile.user = await client.Users[userId].Request().GetAsync();
                profile.photo = "";

                // Graph query for user photo by userId.
                var photo = await client.Users[userId].Photo.Content.Request().GetAsync();

                if (photo != null)
                {
                    // Convert to MemoryStream for ease of rendering.
                    using (MemoryStream stream = (MemoryStream)photo)
                    {
                        string toBase64Photo = Convert.ToBase64String(stream.ToArray());
                        profile.photo = "data:image/jpeg;base64, " + toBase64Photo;
                    }
                }
            }
            catch (Exception)
            {
                // no photo
            }
            finally
            {

            }

            return View(profile);
        }
    }
}