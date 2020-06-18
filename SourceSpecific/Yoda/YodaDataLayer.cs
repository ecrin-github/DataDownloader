using Dapper.Contrib.Extensions;
using Dapper;
using Npgsql;
using System;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Collections.Generic;

namespace DataDownloader.yoda
{
	public class YodaDataLayer
	{
		private string _ctg_connString;
		private string _isrctn_connString;
		private string _yoda_pp_connString;

		/// <summary>
		/// Parameterless constructor is used to automatically build
		/// the connection string, using an appsettings.json file that 
		/// has the relevant credentials (but which is not stored in GitHub).
		/// The json file also includes the root folder path, which is
		/// stored in the class's folder_base property.
		/// </summary>
		public YodaDataLayer()
		{
			IConfigurationRoot settings = new ConfigurationBuilder()
				.SetBasePath(AppContext.BaseDirectory)
				.AddJsonFile("appsettings.json")
				.Build();

			NpgsqlConnectionStringBuilder builder = new NpgsqlConnectionStringBuilder();
			builder.Host = settings["host"];
			builder.Username = settings["user"];
			builder.Password = settings["password"];

			builder.Database = "ctg";
			builder.SearchPath = "ad";
			_ctg_connString = builder.ConnectionString;

			builder.Database = "isrctn";
			builder.SearchPath = "ad";
			_isrctn_connString = builder.ConnectionString;

			builder.Database = "yoda";
			builder.SearchPath = "pp";
			_yoda_pp_connString = builder.ConnectionString;

			// example appsettings.json file...
			// the only values required are for...
			// {
			//	  "host": "host_name...",
			//	  "user": "user_name...",
			//    "password": "user_password...",
			//	  "folder_base": "C:\\MDR JSON\\Object JSON... "
			// }
		}
		

		//public IEnumerable<Summary> FetchYodaSummaries()
		//{
		//	using (var conn = new NpgsqlConnection(_connString))
		//	{
		//		string sql_string = "Select id, nct_number, generic_name, study_name, ";
		//		sql_string += "details_link, enrolment_num, csr_link from pp.study_list ";
		//		sql_string += "order by id ";
		//		return conn.Query<Summary>(sql_string);
		//	}
		//}


		public SponsorDetails FetchYodaSponsorFromNCT(string nct_id)
		{
			using (var conn = new NpgsqlConnection(_ctg_connString))
			{
				string sql_string = "Select organisation_id as org_id, organisation_name as org_name from ad.study_contributors ";
				sql_string += "where sd_id = '" + nct_id + "' and contrib_type_id = 54;";
				return conn.QueryFirstOrDefault<SponsorDetails>(sql_string);
			}
		}


		public SponsorDetails FetchYodaSponsorFromISRCTN(string isrctn_id)
		{
			using (var conn = new NpgsqlConnection(_isrctn_connString))
			{
				string sql_string = "Select organisation_id as org_id, organisation_name as org_name from ad.study_contributors ";
				sql_string += "where sd_id = '" + isrctn_id + "' and contrib_type_id = 54;";
				return conn.QueryFirstOrDefault<SponsorDetails>(sql_string);
			}
		}


		public SponsorDetails FetchYodaSponsorDetailsFromTable(string sd_id)
		{
			using (var conn = new NpgsqlConnection(_yoda_pp_connString))
			{
				string sql_string = "Select sponsor_org_id as org_id, sponsor_org as org_name from pp.not_registered ";
				sql_string += "where link_param = '" + sd_id + "'";
				return conn.QueryFirstOrDefault<SponsorDetails>(sql_string);
			}
		}
	}
}
