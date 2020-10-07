using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace DataDownloader.who
{
	public static class WHOHelpers
	{

		public static List<string> split_string(string instring)
		{
			if (string.IsNullOrEmpty(instring))
			{
				return null;
			}
			else
			{
				string string_list = StringHelpers.tidy_string(instring);
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
				string id_list = StringHelpers.tidy_string(instring);
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
