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
			string interim_string = StringHelpers.tidy_string(instring);
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
					else if (Regex.Match(datestring, @"^(0?[1-9]|1\d|2\d|3[0-1]) (Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec) (19|20)\d{2}$").Success)
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
					else if (Regex.Match(datestring, @"^(0?[1-9]|1\d|2\d|3[0-1]) (January|February|March|April|May|June|July|August|September|October|November|December) (19|20)\d{2}$").Success)
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


		public static DateTime? FetchDateTimeFromISO(string iso_string)
        {
			DateTime? dt = null;
			// iso_string assumed to be in format yyyy-mm-dd
			int year = Int32.Parse(iso_string.Substring(0, 4));
			int month = Int32.Parse(iso_string.Substring(5, 2));
			int day = Int32.Parse(iso_string.Substring(8,2));
			dt = new DateTime(year, month, day);
			return dt;

		}


        public static SplitDate GetDateParts(string dateString)
		{
			// input date string is in the form of "<month name> day, year"
			// or in some cases in the form "<month name> year"
			// split the string on the comma
			string year_string, month_name, day_string;
			int? year_num, month_num, day_num;

			int comma_pos = dateString.IndexOf(',');
			if (comma_pos > 0)
			{
				year_string = dateString.Substring(comma_pos + 1).Trim();
				string first_part = dateString.Substring(0, comma_pos).Trim();

				// first part should split on the space
				int space_pos = first_part.IndexOf(' ');
				day_string = first_part.Substring(space_pos + 1).Trim();
				month_name = first_part.Substring(0, space_pos).Trim();
			}
			else
			{
				int space_pos = dateString.IndexOf(' ');
				year_string = dateString.Substring(space_pos + 1).Trim();
				month_name = dateString.Substring(0, space_pos).Trim();
				day_string = "";
			}

			// convert strings into integers
			if (int.TryParse(year_string, out int y)) year_num = y; else year_num = null;
			month_num = GetMonthAsInt(month_name);
			if (int.TryParse(day_string, out int d)) day_num = d; else day_num = null;
			string month_as3 = ((MonthsAs3)month_num).ToString();

			// get date as string
			string date_as_string;
			if (year_num != null && month_num != null && day_num != null)
			{
				date_as_string = year_num.ToString() + " " + month_as3 + " " + day_num.ToString();
			}
			else if (year_num != null && month_num != null && day_num == null)
			{
				date_as_string = year_num.ToString() + ' ' + month_as3;
			}
			else if (year_num != null && month_num == null && day_num == null)
			{
				date_as_string = year_num.ToString();
			}
			else
			{
				date_as_string = null;
			}

			return new SplitDate(year_num, month_num, day_num, date_as_string);
		}


		public static DateTime? FetchDateTimeFromDateString(string dateString)
        {
			SplitDate sd = GetDateParts(dateString);
			if (sd.year != null && sd.month != null && sd.day != null)
			{
				return new DateTime((int)sd.year, (int)sd.month, (int)sd.day);
			}
            else
            {
				return null;
            }

		}
	}


	public class SplitDate
	{
		public int? year;
		public int? month;
		public int? day;
		public string date_string;

		public SplitDate(int? _year, int? _month, int? _day, string _date_string)
		{
			year = _year;
			month = _month;
			day = _day;
			date_string = _date_string;
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
