using Microsoft.Extensions.Configuration;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using Traktor.Core.Domain.Trakt;
using System.Threading.Tasks;
using System.Threading;
using System.Collections;

namespace Traktor.Core.Services
{
    public class TraktAPIException : Exception
    {
        IRestResponse response;
        public TraktAPIException(IRestResponse response, string message = null, Exception innerException = null) : base(message, innerException)
        {
            this.response = response;
        }

        public enum APIStatus
        {
            AuthenticatedRequired,
            Error
        }

        public APIStatus Status {
            get
            {
                switch (this.response.StatusCode)
                {
                    case System.Net.HttpStatusCode.Unauthorized:
                        return APIStatus.AuthenticatedRequired;
                    default:
                        return APIStatus.Error;
                }
            }
        }
    }

    public partial class TraktService
    {
        // partial class required with clientId (string) and clientSecret (string)
        private RestClient client;
        private Configuration config;

        public string AccessToken 
        { 
            get
            {
                return config.AppSettings?.Settings["trakt.accesstoken"]?.Value;
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                    client.AddDefaultHeader("Authorization", $"Bearer {value}");

                if (!config.AppSettings.Settings.AllKeys.Any(x => x == "trakt.accesstoken"))
                    config.AppSettings.Settings.Add("trakt.accesstoken", value);
                else config.AppSettings.Settings["trakt.accesstoken"].Value = value;
                config.Save(ConfigurationSaveMode.Modified, true);
            }
        }

        public string RefreshToken 
        { 
            get
            {
                return config.AppSettings?.Settings["trakt.refreshtoken"]?.Value;
            }
            set
            {
                if (!config.AppSettings.Settings.AllKeys.Any(x => x == "trakt.refreshtoken"))
                    config.AppSettings.Settings.Add("trakt.refreshtoken", value);
                else config.AppSettings.Settings["trakt.refreshtoken"].Value = value;
                config.Save(ConfigurationSaveMode.Modified, true);
            }
        }

        public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);

        public TraktService()
        {
            this.config = (System.Diagnostics.Debugger.IsAttached) ? ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None) : ConfigurationManager.OpenExeConfiguration(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
            //IConfiguration config = new ConfigurationBuilder().AddUserSecrets<TraktService>().Build();

            //clientId = config["TraktApi:clientId"] ?? ConfigurationManager.AppSettings["trakt.clientId"];
            //clientSecret = config["TraktApi:clientSecret"] ?? ConfigurationManager.AppSettings["trakt.clientSecret"];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                throw new Exception("trakt api clientId or clientSecret missing");

            client = new RestClient(ConfigurationManager.AppSettings["trakt.apiendpoint"] ?? "https://api.trakt.tv");
            client.AddDefaultHeader("Content-Type", "application/json");
            client.AddDefaultHeader("trakt-api-key", clientId);
            client.AddDefaultHeader("trakt-api-version", "2");

            if (!string.IsNullOrEmpty(AccessToken))
                client.AddDefaultHeader("Authorization", $"Bearer {AccessToken}");
        }

        public IEnumerable<T> Many<T>(object parameters = null) where T : TraktAPIObjectBase, new()
        {
            var objInstance = new T();
            return MakeRequest<List<T>>(objInstance.BuildRestRequest(parameters));
        }
        public T One<T>(object parameters = null, object data = null) where T : TraktAPIObjectBase, new()
        {
            var objInstance = new T();
            return MakeRequest<T>(objInstance.BuildRestRequest(parameters), data);
        }

        private T MakeRequest<T>(string resource, Method method = Method.GET, object data = null) where T : new()
        {
            var request = new RestRequest(resource, method);
            if (data != null)
                request.AddJsonBody(data);

            return MakeRequest<T>(request);
        }

        private T MakeRequest<T>(RestRequest request, object data = null) where T : new()
        {
            if (data != null)
                request.AddJsonBody(data);

            var response = client.Execute<T>(request);
            
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                if (!string.IsNullOrEmpty(RefreshToken))
                {
                    AuthenticateOAuth(RefreshToken, true);

                    response = client.Execute<T>(request);
                }
            }

            if (response.IsSuccessful)
            {
                if (int.TryParse(response.Headers.FirstOrDefault(x => x.Name == "X-Pagination-Page-Count")?.Value.ToString(), out var pageCount))
                {
                    var list = response.Data as IList;

                    if (int.TryParse(response.Headers.FirstOrDefault(x => x.Name == "X-Pagination-Page").Value.ToString(), out var page))
                    {
                        while (page < pageCount)
                        {
                            request.AddOrUpdateParameter("page", page + 1);

                            var paginatedResponse = client.Execute<T>(request);
                            if (response.IsSuccessful)
                            {
                                foreach (var item in paginatedResponse.Data as IList)
                                    list.Add(item);
                            }

                            page++;
                        }

                        return (T)list;
                    }
                    
                }

                return response.Data;
            }
            throw new TraktAPIException(response, $"{response.StatusCode} - {response.StatusDescription} - {response.ErrorMessage}", response.ErrorException);
        }

        public bool AuthenticateDeviceWaitForActivation(DeviceAuthentication dAuth)
        {
            var pollRequest = new RestRequest("oauth/device/token", Method.POST);
            pollRequest.AddJsonBody(new
            {
                code = dAuth.device_code,
                client_id = clientId,
                client_secret = clientSecret
            });

            int elapsed = 0;
            var interval = dAuth.interval;
            while (elapsed < dAuth.expires_in)
            {
                var pollResponse = client.Execute<dynamic>(pollRequest);
                switch ((int)pollResponse.StatusCode)
                {
                    case 200: // tokens in.
                        var accessToken = pollResponse.Data["access_token"];
                        var refreshToken = pollResponse.Data["refresh_token"];

                        AccessToken = accessToken;
                        RefreshToken = refreshToken;
                        return true;
                    case 400: // Pending, keep polling.
                        break;
                    case 429: // Slow down.
                        interval++;
                        break;
                    default: // Anything else, throw exception?
                        throw new TraktAPIException(pollResponse, "DeviceAuthentication: Polling aborted");
                }

                Thread.Sleep(interval * 1000);
                elapsed += interval;
            }
            return false;
        }

        public DeviceAuthentication AuthenticateDevice()
        {
            var request = new RestRequest("oauth/device/code", Method.POST);
            request.AddJsonBody(new
            {
                client_id = clientId,
            });

            var response = client.Execute<DeviceAuthentication>(request);
            if (response.IsSuccessful)
            {
                return response.Data;
            }
            else throw new TraktAPIException(response, "Failed to get authentication code.");
        }

        public void AuthenticateOAuth(string code, bool refresh = false)
        {
            var request = new RestRequest("oauth/token", Method.POST);
            request.AddJsonBody(new
            {
                code = !refresh ? code : null,
                refresh_token = refresh? code : null,
                client_id = clientId,
                client_secret = clientSecret,
                redirect_uri = "urn:ietf:wg:oauth:2.0:oob",
                grant_type = !refresh ? "authorization_code" : "refresh_token"
            });


            var response = client.Execute<dynamic>(request);
            if (response.IsSuccessful)
            {
                AccessToken = response.Data["access_token"];
                RefreshToken = response.Data["refresh_token"];
            }
            else throw new TraktAPIException(response, "Failed to authenticate");
        }
    }
}
