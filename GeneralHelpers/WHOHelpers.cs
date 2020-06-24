using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace DataDownloader
{
	public static class WHOHelpers
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


		public static List<string> split_string(string instring)
		{
			if (string.IsNullOrEmpty(instring))
			{
				return null;
			}
			else
			{
				string string_list = tidy_string(instring);
				if (string.IsNullOrEmpty(string_list))
				{
					return null;
				}
				else
				{
					return string_list.Split(";").ToList();
				}
			}
		}


		public static List<string> split_and_dedup_string(string instring)
		{

			if (string.IsNullOrEmpty(instring))
			{
				return null;
			}
			else
			{
				string id_list = tidy_string(instring);
				if (string.IsNullOrEmpty(id_list))
				{
					return null;
				}
				else
				{
					List<string> outstrings = new List<string>();
					List<string> instrings = id_list.Split(";").ToList();
					foreach (string s in instrings)
					{
						if (outstrings.Count == 0)
						{
							outstrings.Add(s);
						}
						else
						{
							bool add_string = true;
							foreach (string s2 in outstrings)
							{
								if (s2 == s)
								{
									add_string = false;
									break;
								}
							}
							if (add_string) outstrings.Add(s);
						}
					}
					return outstrings;
				}
			}

		}

	}
}
