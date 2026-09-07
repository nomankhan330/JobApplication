using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using BusinessLogic.Helper;
using BusinessLogic.Interfaces;
using BusinessLogic.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace BusinessLogic.Services
{
    public class SessionHelper : ISessionHelper
    {
        private readonly ISession _session;
        private const string ACCOUNT_ID = "act_id";
        private const string ACCOUNT_NAME = "act_name";
        private const string Datasource_value = "Datasource_value";
        private const string Database_value = "Database_value";
        private const string ExpiryDate_value = "ExpiryDate_value";
        private const string Password_value = "Password_value";
        private const string Userid_value = "Userid_value";
        private const string LOGIN_ID = "lgnid";
        private const string LOGIN_TYPE = "lgntype";
        private const string USER_TYPE = "usertype";
        private const string REFERENCE_ID = "refid";
        private const string USER_NAME = "usrnme";
        private const string COST_CENTERS = "cstcent";
        private const string COST_CENTERIDS = "cstcentid";
        private const string REGION_ID = "rgnid";
        private const string TOKEN = "token";
        private const string RIGHTSLIST = "rightlist";
        private const string MENULIST = "menuList";

        // Added: photo key
        private const string PHOTO = "photo";

        public SessionHelper(IHttpContextAccessor context)
        {
            _session = context.HttpContext.Session;
        }
        
        public int AccountId
        {
            get
            {
                if (_session.TryGetValue(ACCOUNT_ID, out var value))
                {
                    return DataHelper.intParse(GetString(value));
                }
                else
                {
                    return 0;
                }
            }
            set
            {
                _session.Set(ACCOUNT_ID, GetBytes(value.ToString()));
            }
        }

        public string AccountName
        {
            get
            {
                if (_session.TryGetValue(ACCOUNT_NAME, out var value))
                {
                    return GetString(value);
                }
                else
                {
                    return "";
                }
            }
            set
            {
                _session.Set(ACCOUNT_NAME, GetBytes(value));
            }
        }

        public string Token
        {
            get
            {
                if (_session.TryGetValue(TOKEN, out var value))
                {
                    return GetString(value);
                }
                else
                {
                    return "";
                }
            }
            set
            {
                _session.Set(TOKEN, GetBytes(value));
            }
        }

        public string Datasource
        {
            get
            {
                if (_session.TryGetValue(Datasource_value, out var value))
                {
                    return DataHelper.stringParse(GetString(value));
                }
                else
                {
                    return "";
                }
            }
            set
            {
                _session.Set(Datasource_value, GetBytes(value.ToString()));
            }
        }

        public string Databasename
        {
            get
            {
                if (_session.TryGetValue(Database_value, out var value))
                {
                    return DataHelper.stringParse(GetString(value));
                }
                else
                {
                    return "";
                }
            }
            set
            {
                _session.Set(Database_value, GetBytes(value.ToString()));
            }
        }

        public DateTime? ExpiryDate
        {
            get
            {
                if (_session.TryGetValue(ExpiryDate_value, out var value))
                {
                    return DataHelper.dateParse(value);
                }
                else
                {
                    return null;
                }
            }
            set
            {
                _session.Set(ExpiryDate_value, GetBytes(value.ToString()));
            }
        }

        public string Dbpassword
        {
            get
            {
                if (_session.TryGetValue(Password_value, out var value))
                {
                    return DataHelper.stringParse(GetString(value));
                }
                else
                {
                    return "";
                }
            }
            set
            {
                _session.Set(Password_value, GetBytes(value.ToString()));
            }
        }
        
        public string Dbuserid
        {
            get
            {
                if (_session.TryGetValue(Userid_value, out var value))
                {
                    return DataHelper.stringParse(GetString(value));
                }
                else
                {
                    return "";
                }
            }
            set
            {
                _session.Set(Userid_value, GetBytes(value.ToString()));
            }
        }

        public int LoginId
        {
            get
            {
                if (_session.TryGetValue(LOGIN_ID, out var value))
                {
                    return DataHelper.intParse(GetString(value));
                }
                else
                {
                    return 0;
                }
            }
            set
            {
                _session.Set(LOGIN_ID, GetBytes(value.ToString()));
            }
        }

        public int LoginType
        {
            get
            {
                if (_session.TryGetValue(LOGIN_TYPE, out var value))
                {
                    return DataHelper.intParse(GetString(value));
                }
                else
                {
                    return 0;
                }
            }
            set
            {
                _session.Set(LOGIN_TYPE, GetBytes(value.ToString()));
            }
        }

        public int UserType
        {
            get
            {
                if (_session.TryGetValue(USER_TYPE, out var value))
                {
                    return DataHelper.intParse(GetString(value));
                }
                else
                {
                    return 0;
                }
            }
            set
            {
                _session.Set(USER_TYPE, GetBytes(value.ToString()));
            }
        }

        public string UserName
        {
            get
            {
                if (_session.TryGetValue(USER_NAME, out var value))
                {
                    return GetString(value);
                }
                else
                {
                    return "";
                }
            }
            set
            {
                _session.Set(USER_NAME, GetBytes(value.ToString()));
            }
        }

        public string CostCenters
        {
            get
            {
                if (_session.TryGetValue(COST_CENTERS, out var value))
                {
                    return GetString(value);
                }
                else
                {
                    return "";
                }
            }
            set
            {
                _session.Set(COST_CENTERS, GetBytes(value.ToString()));
            }
        }

        public string CostCenterIds
        {
            get
            {
                if (_session.TryGetValue(COST_CENTERIDS, out var value))
                {
                    return GetString(value);
                }
                else
                {
                    return "";
                }
            }
            set
            {
                _session.Set(COST_CENTERIDS, GetBytes(value.ToString()));
            }
        }

        public int ReferenceId
        {
            get
            {
                if (_session.TryGetValue(REFERENCE_ID, out var value))
                {
                    return DataHelper.intParse(GetString(value));
                }
                else
                {
                    return 0;
                }
            }
            set
            {
                _session.Set(REFERENCE_ID, GetBytes(value.ToString()));
            }
        }

        // Added Photo property implemented like UserName
        public string Photo
        {
            get
            {
                if (_session.TryGetValue(PHOTO, out var value))
                {
                    return GetString(value);
                }
                else
                {
                    return "";
                }
            }
            set
            {
                _session.Set(PHOTO, GetBytes(value.ToString()));
            }
        }

        public void Login(int AccountId, int LoginId, string UserName, int ReferenceId)
        {
            _session.Set(ACCOUNT_ID, GetBytes(AccountId.ToString()));
            _session.Set(LOGIN_ID, GetBytes(LoginId.ToString()));
            _session.Set(USER_NAME, GetBytes(UserName));
            _session.Set(REFERENCE_ID, GetBytes(ReferenceId.ToString()));
        }

        // Updated Login overload to accept Photo (keeps existing behavior for older calls removed)
        public void Login(int AccountId, int LoginId, string UserName, int ReferenceId, string Photo)
        {
            _session.Set(ACCOUNT_ID, GetBytes(AccountId.ToString()));
            _session.Set(LOGIN_ID, GetBytes(LoginId.ToString()));
            _session.Set(USER_NAME, GetBytes(UserName));
            _session.Set(REFERENCE_ID, GetBytes(ReferenceId.ToString()));
            _session.Set(PHOTO, GetBytes(Photo ?? ""));
        }

        public void RemoveSession(string key)
        {
            _session.Remove(key);
        }

        public void Logout()
        {
            RemoveSession(ACCOUNT_ID);
            RemoveSession(LOGIN_ID);
            RemoveSession(USER_NAME);
            RemoveSession(LOGIN_TYPE);
            RemoveSession(USER_TYPE);
            RemoveSession(REGION_ID);

            // Remove photo as part of logout
            RemoveSession(PHOTO);
        }

        public void Set(string key, string value)
        {
            _session.Set(key, GetBytes(value));
        }

        public string Get(string key)
        {
            if (_session.TryGetValue(key, out var value))
            {
                return GetString(value);
            }
            else
            {
                return "";
            }
        }

        private byte[] GetBytes(string value)
        {
            return Encoding.UTF8.GetBytes(value);
        }

        private string GetString(byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
