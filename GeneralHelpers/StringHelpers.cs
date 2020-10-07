using System;
using System.Collections.Generic;
using System.Text;

namespace DataDownloader
{

	public static class StringHelpers
	{
		public static string tidy_string(string instring)
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



		public static void SendFeedback(string message, string identifier = "")
		{
			string dt_string = DateTime.Now.ToShortDateString() + " : " + DateTime.Now.ToShortTimeString() + " :   ";
			System.Console.WriteLine(dt_string + message + identifier);
		}

		public static void SendHeader(string message)
		{
			string dt_string = DateTime.Now.ToShortDateString() + " : " + DateTime.Now.ToShortTimeString() + " :   ";
			System.Console.WriteLine("");
			System.Console.WriteLine(dt_string + "**** " + message + " ****");
		}

		public static void SendError(string message)
		{
			string dt_string = DateTime.Now.ToShortDateString() + " : " + DateTime.Now.ToShortTimeString() + " :   ";
			System.Console.WriteLine("");
			System.Console.WriteLine("+++++++++++++++++++++++++++++++++++++++");
			System.Console.WriteLine(dt_string + "***ERROR*** " + message);
			System.Console.WriteLine("+++++++++++++++++++++++++++++++++++++++");
			System.Console.WriteLine("");
		}
	}
}
