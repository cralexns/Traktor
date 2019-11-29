using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Traktor.Core.Domain.Trakt
{
    public abstract class TraktAPIObjectBase
    {
        public class TraktAPIRequest
        {
            public string[] Formats { get; set; }

            public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

            public TraktAPIRequest(string format)
            {
                this.Formats = new[] { format };
            }

            public TraktAPIRequest(params string[] formats)
            {
                this.Formats = formats;
            }

            public string BuildAction(object parameters)
            {
                var properties = parameters?.GetType().GetProperties() ?? null;
                if (!(properties?.Any() ?? false))
                    return this.Formats.FirstOrDefault();

                var result = this.Formats.FirstOrDefault(x=> properties.Select(y=> $"{{{y.Name}}}").All(y=>x.Contains(y)));
                if (string.IsNullOrEmpty(result))
                    throw new InvalidOperationException("Couldn't find matching action format for parameters.");

                foreach (var property in properties)
                {
                    result = result.Replace(
                        $"{{{property.Name}}}",
                        property.GetValue(parameters).ToString());
                }

                return result;
            }
        }

        public TraktAPIObjectBase(TraktAPIRequest request, Method method = Method.GET)
        {
            this.Request = request;
            this.RestSharpMethod = method;
        }

        public TraktAPIObjectBase(string action, Method method = Method.GET)
        {
            this.Request = new TraktAPIRequest(action);
            this.RestSharpMethod = method;
        }

        protected TraktAPIRequest Request { get; set; }
        private Method RestSharpMethod { get; set; }

        private string GetAction(object parameters)
        {
            return this.Request.BuildAction(parameters);
        }

        public RestRequest BuildRestRequest(object parameters)
        {
            var request = new RestRequest(GetAction(parameters), this.RestSharpMethod);
            foreach (var header in this.Request.Headers)
            {
                request.AddHeader(header.Key, header.Value);
            }

            return request;
        }
    }
}
