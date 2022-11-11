using System;
using System.Linq;
using System.Security.Cryptography;

namespace DataDownloader
{

    public class StringHelpers
    {
        LoggingHelper _logging_helper;

        public StringHelpers(LoggingHelper logging_helper)
        {
            _logging_helper = logging_helper;
        }

        public string tidy_string(string instring)
        {
            string return_value = null;
            if (instring != null && instring != "NULL" && instring != "null"
                                && instring != "\"NULL\"" && instring != "\"null\"")
            {
                if (!instring.StartsWith('"'))
                {
                    char[] chars1 = { ' ', ';' };
                    instring = instring.Trim(chars1);
                }
                else
                {
                    char[] chars2 = { '"', ' ', ';' };
                    instring = instring.Trim(chars2);
                }
                return_value = (instring == "") ? null : instring;
            }
            return return_value;
        }

        public string CreateMD5(string input)
        {
            // Use input string to calculate MD5 hash
            using (MD5 md5 = MD5.Create())
            {
                // return as full 32 character hex string - to avoid slashes 
                // and other odd characters in the derived file name 

                byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
                byte[] hashbytes = md5.ComputeHash(inputBytes);
                return string.Concat(hashbytes.Select(x => x.ToString("X2"))).ToLower();

                // return as base64 string
                // 16 bytes = (5*4) characters + XX==, 
                // 24 rather than 32 hex characters
                //return Convert.ToBase64String(hashBytes);
            }
        }
    }
}
