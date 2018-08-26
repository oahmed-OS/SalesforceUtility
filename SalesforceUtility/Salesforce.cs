using FuelSDK;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;

namespace SalesforceUtility
{
    // <summary>
    // A smalll wrapper around the SFMC.FuelSDK 
    // for easy calls to Exact Target
    // </summary>
    public class Salesforce
    {
        private ETClient SFClient;

        private readonly string SFBaseAuthUrl;

        public Salesforce(string clientId, string clientSecret)
        {
            //Set TLS to 1.2
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            SFBaseAuthUrl = "https://auth.exacttargetapis.com/v1/requestToken?legacy=1";

            //Build ExactTarget Client
            SFClient = new ETClient(BuildAuthStub(clientId, 
                clientSecret));
        }

        public Salesforce(string clientId, string clientSecret, string authUrl)
        {
            //Set TLS to 1.2
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            SFBaseAuthUrl = authUrl;

            //Build ExactTarget Client
            SFClient = new ETClient(BuildAuthStub(clientId,
                clientSecret));
        }

        private NameValueCollection BuildAuthStub(string clientId, string clientSecret)
        {
            NameValueCollection authStub = new NameValueCollection();
            authStub.Add("", clientId);
            authStub.Add("", clientSecret);
            authStub.Add("", SFBaseAuthUrl);
            return authStub;

        }

        public ETSubscriber AddSubscriberToLists(string email, int[] lists)
        {
            var addSubscriber = new ETSubscriber
            {
                AuthStub = SFClient,
                SubscriberKey = email,
                EmailAddress = email,
                Lists = GenerateSubscriberList(lists).ToArray(),
                Attributes = new[] { new ETProfileAttribute { Name = "Source", Value = "API" } }
            };
            var response = addSubscriber.Post();

            if (response.Status && response.Results.Length > 0)
                return (ETSubscriber)response.Results[0].Object;

            if (response.Results.Length > 0 && response.Results[0].ErrorCode == 12014)
                UpdateSubscriber(addSubscriber);

            return null;
        }

        public ETSubscriber UpdateSubscriber(ETSubscriber subscriber)
        {
            if (String.IsNullOrEmpty(subscriber.SubscriberKey))
            {
                return null;
            }

            subscriber.AuthStub = SFClient;
            var response = subscriber.Patch();

            if (response.Status && response.Results.Length > 0)
                return (ETSubscriber)response.Results[0].Object;

            return null;
        }

        public bool DeleteSubscriber(string email, int[] lists = null)
        {
            var deleteUser = new ETSubscriber
            {
                AuthStub = SFClient,
                SubscriberKey = email,
                Lists = GenerateSubscriberList(lists)
            };
            var response = deleteUser.Delete();

            return response.Status;
        }

        public bool Unsubscribe(string email, int[] lists = null)
        {

            var patchUser = new ETSubscriber
            {
                AuthStub = SFClient,
                SubscriberKey = email,
                Lists = GenerateSubscriberList(lists, false)
            };

            //Unsubscribe from everything if no lists specified
            if (lists == null)
                patchUser.Status = SubscriberStatus.Unsubscribed;

            var response = patchUser.Patch();

            return response.Status;
        }

        public bool Resubscribe(string email, int[] lists = null)
        {
            var patchUser = new ETSubscriber
            {
                AuthStub = SFClient,
                SubscriberKey = email,
                Lists = GenerateSubscriberList(lists)
            };

            //Resubscribe to everything if no lists specified
            if (lists == null)
                patchUser.Status = SubscriberStatus.Active;

            var response = patchUser.Patch();

            return response.Status;
        }

        public ETSubscriber GetSubscriber(string email)
        {

            var getUser = new ETSubscriber
            {
                AuthStub = SFClient,
                SearchFilter = new SimpleFilterPart
                {
                    Property = "SubscriberKey",
                    SimpleOperator = SimpleOperators.equals,
                    Value = new[] { email }
                }
            };
            var response = getUser.Get();

            if (response.Status && response.Results.Length > 0)
                return (ETSubscriber)response.Results[0];

            return null;
        }

        public ETList GetList(string listName)
        {
            var getList = new ETList
            {
                AuthStub = SFClient,
                SearchFilter = new SimpleFilterPart
                {
                    Property = "ListName",
                    SimpleOperator = SimpleOperators.equals,
                    Value = new[] { listName }
                }
            };
            var response = getList.Get();

            if (response.Status && response.Results.Length > 0)
                return (ETList)response.Results[0];

            return null;
        }

        //Reference: https://stackoverflow.com/questions/6470554/how-to-convert-object-to-a-more-specifically-typed-array
        public List<ETList> GetLists(int folderId)
        {
            var getLists = new ETList
            {
                AuthStub = SFClient
            };
            var response = getLists.Get();

            if (response.Status && response.Results.Length > 0)
            {
                //Convert from APIObject[] to List<ETList> -- Cannot do new List<ETList>((ETList[])response.Results) as this returns an error
                Array conversionArray = Array.CreateInstance(typeof(ETList), response.Results.Length);
                Array.Copy(response.Results, conversionArray, response.Results.Length);
                List<ETList> lists = new List<ETList>((ETList[])conversionArray);

                return lists.Where(e => e.FolderID == folderId).ToList(); ;
            }

            return null;
        }

        public List<ETListSubscriber> GetSubscriberLists(string email)
        {
            var getLists = new ETListSubscriber
            {
                AuthStub = SFClient,
                SearchFilter = new SimpleFilterPart
                {
                    Property = "SubscriberKey",
                    SimpleOperator = SimpleOperators.equals,
                    Value = new[] { email }
                }
            };

            var response = getLists.Get();

            if (response.Status && response.Results.Length > 0)
            {
                Array conversionArray = Array.CreateInstance(typeof(ETListSubscriber), response.Results.Length);
                Array.Copy(response.Results, conversionArray, response.Results.Length);
                return new List<ETListSubscriber>((ETListSubscriber[])conversionArray);
            }
            return null;

        }

        private ETSubscriberList[] GenerateSubscriberList(int[] lists, bool status = true)
        {
            if (lists == null)
                return null;

            List<ETSubscriberList> result = new List<ETSubscriberList>();
            foreach (var list in lists)
            {
                ETSubscriberList tmp = new ETSubscriberList();
                tmp.ID = list;
                tmp.IDSpecified = true;
                tmp.Status = SubscriberStatus.Active;
                tmp.StatusSpecified = true;
                if (!status)
                    tmp.Status = SubscriberStatus.Unsubscribed;

                result.Add(tmp);
            }

            return result.ToArray();
        }
    }
}
