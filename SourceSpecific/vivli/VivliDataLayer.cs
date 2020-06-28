using Dapper.Contrib.Extensions;
using Dapper;
using Npgsql;
using System;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Collections.Generic;
using PostgreSQLCopyHelper;

namespace DataDownloader.vivli
{

	public class VivliDataLayer
	{
		private string connString;

		/// <summary>
		/// Parameterless constructor is used to automatically build
		/// the connection string, using an appsettings.json file that 
		/// has the relevant credentials (but which is not stored in GitHub).
		/// The json file also includes the root folder path, which is
		/// stored in the class's folder_base property.
		/// </summary>
		public VivliDataLayer()
		{
			IConfigurationRoot settings = new ConfigurationBuilder()
				.SetBasePath(AppContext.BaseDirectory)
				.AddJsonFile("appsettings.json")
				.Build();

			NpgsqlConnectionStringBuilder builder = new NpgsqlConnectionStringBuilder();
			builder.Host = settings["host"];
			builder.Username = settings["user"];
			builder.Password = settings["password"];

			builder.Database = "vivli";
			builder.SearchPath = "pp";
			connString = builder.ConnectionString;

			// example appsettings.json file...
			// the only values required are for...
			// {
			//	  "host": "host_name...",
			//	  "user": "user_name...",
			//    "password": "user_password...",
			//	  "folder_base": "C:\\MDR JSON\\Object JSON... "
			// }
		}

		public void SetUpParameterTable()
        {
			using (var conn = new NpgsqlConnection(connString))
			{
				string sql_string = "Drop table if exists pp.api_urls;";
				conn.Execute(sql_string);

				sql_string = @"Create table pp.api_urls(
                                    id int,
                                    name varchar,
                                    type varchar,
                                    doi varchar,
                                    vivli_url varchar);";
				conn.Execute(sql_string);
			}
		}


		public void SetUpStudiesTable()
		{
			using (var conn = new NpgsqlConnection(connString))
			{
				string sql_string = "Drop table if exists pp.studies;";
				conn.Execute(sql_string);

				sql_string = @"Create table pp.studies(
                         id int,
                         vivli_id varchar,
                         study_title varchar,
                         acronym varchar,
                         pi_firstname varchar,
                         pi_lastname varchar,
                         pi_orcid_id varchar,
                         protocol_id varchar,
                         sponsor varchar,
                         nct_id  varchar,
		                 dobj_id  varchar,
		                 data_prot_level varchar,
		                 save_ipd_for_future boolean, 
		                 ipd_package_id varchar,
		                 metadata_package_id varchar,
		                 ipd_content_type varchar,
		                 study_metadata_doi varchar,
		                 study_req_behav varchar,
	                     bulk_upload_type varchar,
		                 downloadable_ipd_package boolean, 
		                 ipd_data_supp_on_submission boolean, 
		                 doi_stem varchar,
		                 ipd_package_doi varchar,
		                 additional_info varchar,
		                 date_created timestamptz,
		                 date_updated timestamptz);";
					conn.Execute(sql_string);
			}
		}


		public void SetUpPackagesTable()
		{
			using (var conn = new NpgsqlConnection(connString))
			{
				string sql_string = "Drop table if exists pp.data_packages;";
				conn.Execute(sql_string);

				sql_string = @"Create table pp.data_packages(
                         id int,
                         vivli_id varchar,
                         vivli_study_id varchar,
                         package_doi varchar,
                         doi_stem varchar,
                         package_title varchar,
                         status varchar,
                         downloadable boolean,
                         files_dlable_before boolean,
                         package_type varchar,
                         sec_anal_dois varchar);";
				conn.Execute(sql_string);
			}
		}


		public void SetUpDataObectsTable()
		{
			using (var conn = new NpgsqlConnection(connString))
			{
				string sql_string = "Drop table if exists pp.data_objects;";
				conn.Execute(sql_string);

				sql_string = @"Create table pp.data_objects(
                          id int,
                          package_id int,             
                          object_type varchar,
                          object_name varchar,
                          comment varchar,
                          is_complete boolean, 
                          size_kb varchar,
                          updated timestamptz,
                          package_doi varchar);";
				conn.Execute(sql_string);
			}
		}



		public ulong StoreRecs(PostgreSQLCopyHelper<VivliURL> copyHelper, 
			                        IEnumerable<VivliURL> entities)
		{
			using (var conn = new NpgsqlConnection(connString))
			{
				conn.Open();
				// Returns count of rows written 
				return copyHelper.SaveAll(conn, entities);
			}
		}


		public IEnumerable<VivliURL> FetchVivliApiUrLs()
        {
			using (var conn = new NpgsqlConnection(connString))
			{
				string sql_string = @"Select * from  pp.api_urls";
				//sql_string += @"  where type = 's';";
				return conn.Query<VivliURL>(sql_string);
			}
		}


		public int StoreStudyRecord(VivliRecord vr)
		{
			using (var conn = new NpgsqlConnection(connString))
			{
				return (int)conn.Insert<VivliRecord>(vr);
			}
		}


		public int StorePackageRecord(PackageRecord pr)
		{
			using (var conn = new NpgsqlConnection(connString))
			{
				return (int)conn.Insert<PackageRecord>(pr);
			}
		}


		public ulong StoreObjectRecs(PostgreSQLCopyHelper<ObjectRecord> copyHelper, 
			                             IEnumerable<ObjectRecord> entities)
		{
			using (var conn = new NpgsqlConnection(connString))
			{
				conn.Open();
				// Returns count of rows written 
				return copyHelper.SaveAll(conn, entities);
			}
		}

	}


	public static class CopyHelpers
	{
		public static PostgreSQLCopyHelper<VivliURL> api_url_copyhelper =
			new PostgreSQLCopyHelper<VivliURL>("pp", "api_urls")
				.MapInteger("id", x => x.id)
				.MapVarchar("name", x => x.name)
			    .MapVarchar("type", x => x.type)
				.MapVarchar("doi", x => x.doi)
				.MapVarchar("vivli_url", x => x.vivli_url);


		public static PostgreSQLCopyHelper<ObjectRecord> data_object_copyhelper =
			new PostgreSQLCopyHelper<ObjectRecord>("pp", "data_objects")
				.MapInteger("id", x => x.id)
				.MapInteger("package_id", x => x.package_id)
				.MapVarchar("object_type", x => x.object_type)
				.MapVarchar("object_name", x => x.object_name)
				.MapVarchar("comment", x => x.comment)
				.MapBoolean("is_complete", x => x.is_complete)
				.MapVarchar("size_kb", x => x.size_kb)
				.MapTimeStampTz("updated", x => x.updated)
				.MapVarchar("package_doi", x => x.package_doi);
}


}
