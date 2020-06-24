using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DataDownloader
{
	public static class DateHelpers
	{

		public static int GetMonthAsInt(string month_name)
		{
			try
			{
				return (int)(Enum.Parse<MonthsFull>(month_name));
			}
			catch (ArgumentException)
			{
				return 0;
			}
		}


		public static int GetMonthAs3AsInt(string month_abbrev)
		{
			try
			{
				return (int)(Enum.Parse<MonthsAs3>(month_abbrev));
			}
			catch (ArgumentException)
			{
				return 0;
			}
		}


		public static string iso_date(string instring)
		{
			string interim_string = WHOHelpers.tidy_string(instring);
			if (interim_string == null)
			{
				return null;
			}
			else
			{
				string return_value = null;
				if (interim_string != "1900-01-01" && interim_string != "01/01/1900"
						  && interim_string != "Jan  1 1900" && interim_string != "Jan  1 1900 12:00AM")
				{
					// Make the delimiter constant and remove commas
					string datestring = interim_string.Replace('/', '-').Replace('.', '-').Replace(",", "");
					if (Regex.Match(datestring, @"^(19|20)\d{2}-(0?[1-9]|1[0-2])-(0?[1-9]|1\d|2\d|3[0-1])$").Success)
					{
						// date in form yyyy-(m)m-(d)d
						if (datestring.Length == 10)
						{
							// already OK
							return datestring;
						}
						else
						{
							int dash1 = datestring.IndexOf('-');
							int dash2 = datestring.LastIndexOf('-');
							string year_s = datestring.Substring(0, 4);
							string month_s = datestring.Substring(dash1 + 1, dash2 - dash1 - 1);
							if (month_s.Length == 1) month_s = "0" + month_s;
							string day_s = datestring.Substring(dash2 + 1);
							if (day_s.Length == 1) day_s = "0" + day_s;
							return_value = year_s + "-" + month_s + "-" + day_s;
						}
					}
					else if (Regex.Match(datestring, @"^(0?[1-9]|1\d|2\d|3[0-1])-(0?[1-9]|1[0-2])-(19|20)\d{2}$").Success)
					{
						// date in form (d)d-(m)m-yyyy
						int dash1 = datestring.IndexOf('-');
						int dash2 = datestring.LastIndexOf('-');
						string year_s = datestring.Substring(dash2 + 1);
						string month_s = datestring.Substring(dash1 + 1, dash2 - dash1 - 1);
						if (month_s.Length == 1) month_s = "0" + month_s;
						string day_s = datestring.Substring(0, dash1);
						if (day_s.Length == 1) day_s = "0" + day_s;
						return_value = year_s + "-" + month_s + "-" + day_s;
					}
					else if (Regex.Match(datestring, @"^(0?[1-9]|1\d|2\d|3[0-1]) (Jan|Feb|Mar|Apr|Jun|Jul|Aug|Sep|Oct|Nov|Dec) (19|20)\d{2}$").Success)
					{
						// date in form (d)d MMM yyyy
						int dash1 = datestring.IndexOf(' ');
						int dash2 = datestring.LastIndexOf(' ');
						string year_s = datestring.Substring(dash2 + 1);
						string month = datestring.Substring(dash1 + 1, dash2 - dash1 - 1);
						string month_s = GetMonthAs3AsInt(month).ToString("00");
						string day_s = datestring.Substring(0, dash1);
						if (day_s.Length == 1) day_s = "0" + day_s;
						return_value = year_s + "-" + month_s + "-" + day_s;
					}
					else if (Regex.Match(datestring, @"^(0?[1-9]|1\d|2\d|3[0-1]) (January|February|March|April|June|July|August|September|October|November|December) (19|20)\d{2}$").Success)
					{
						// date in form (d)d MMMM yyyy
						int dash1 = datestring.IndexOf(' ');
						int dash2 = datestring.LastIndexOf(' ');
						string year_s = datestring.Substring(dash2 + 1);
						string month = datestring.Substring(dash1 + 1, dash2 - dash1 - 1);
						string month_s = GetMonthAsInt(month).ToString("00");
						string day_s = datestring.Substring(0, dash1);
						if (day_s.Length == 1) day_s = "0" + day_s;
						return_value = year_s + "-" + month_s + "-" + day_s;
					}
					else
					{
						// to investigate other date forms.....
						return_value = interim_string;
					}
				}
				return return_value;
			}
		}


		public static string GetTimeUnits(string instring)
		{
			string time_string = instring.ToLower();
			string time_units = "";
			if (time_string.Contains("year"))
			{
				time_units = "Years";
			}
			else if (time_string.Contains("month"))
			{
				time_units = "Months";
			}
			else if (time_string.Contains("week"))
			{
				time_units = "Weeks";
			}
			else if (time_string.Contains("day"))
			{
				time_units = "Days";
			}
			else if (time_string.Contains("hour"))
			{
				time_units = "Hours";
			}
			else if (time_string.Contains("min"))
			{
				time_units = "Minutes";
			}
			else
			{
				time_units = "Other (" + time_string + ")";
			}
			return time_units;
		}
	}


	public enum MonthsFull
    {
        January = 1, February, March, April, May, June,
        July, August, September, October, November, December
    };


    public enum MonthsAs3
    {
        Jan = 1, Feb, Mar, Apr, May, Jun,
        Jul, Aug, Sep, Oct, Nov, Dec
    };
}
