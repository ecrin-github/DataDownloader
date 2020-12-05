using System;

namespace DataDownloader
{

    public class StringHelpers
    {
        LoggingDataLayer logging_repo;

        public StringHelpers(LoggingDataLayer _logging_repo)
        {
            logging_repo = _logging_repo;
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
    }
}
