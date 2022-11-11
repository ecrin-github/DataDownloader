using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;

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

        }


        public SponsorDetails FetchSponsorFromNCT(string nct_id)
        {
            using (var conn = new NpgsqlConnection(_ctg_connString))
            {
                string sql_string = "Select organisation_id as org_id, organisation_name as org_name, null from ad.study_contributors ";
                sql_string += "where sd_sid = '" + nct_id + "' and contrib_type_id = 54;";
                return conn.QueryFirstOrDefault<SponsorDetails>(sql_string);
            }
        }


        public StudyDetails FetchStudyDetailsFromNCT(string nct_id)
        {
            using (var conn = new NpgsqlConnection(_ctg_connString))
            {
                string sql_string = "Select sd_sid, display_title, brief_description, study_type_id from ad.studies ";
                sql_string += "where sd_sid = '" + nct_id + "'";
                return conn.QueryFirstOrDefault<StudyDetails>(sql_string);
            }
        }


        public SponsorDetails FetchSponsorFromISRCTN(string isrctn_id)
        {
            using (var conn = new NpgsqlConnection(_isrctn_connString))
            {
                string sql_string = "Select organisation_id as org_id, organisation_name as org_name, null from ad.study_contributors ";
                sql_string += "where sd_sid = '" + isrctn_id + "' and contrib_type_id = 54;";
                return conn.QueryFirstOrDefault<SponsorDetails>(sql_string);
            }
        }


        public StudyDetails FetchStudyDetailsFromISRCTN(string isrctn_id)
        {
            using (var conn = new NpgsqlConnection(_isrctn_connString))
            {
                string sql_string = "Select sd_sid, display_title, brief_description, study_type_id from ad.studies ";
                sql_string += "where sd_sid = '" + isrctn_id + "'";
                return conn.QueryFirstOrDefault<StudyDetails>(sql_string);
            }
        }


        public NotRegisteredDetails FetchNonRegisteredDetailsFromTable(string pp_id)
        {
            using (var conn = new NpgsqlConnection(_yoda_pp_connString))
            {
                string sql_string = @"Select sponsor_id, sponsor_name, short_sponsor_name, short_protocol_id,
                                      title, brief_description, study_type_id 
                                      from pp.not_registered 
                                      where sd_sid = '" + pp_id + "'";
                return conn.QueryFirstOrDefault<NotRegisteredDetails>(sql_string);
            }
        }


        public void AddNewNotRegisteredRecord(string pp_id, string title, string short_sponsor_name, string protid)
        {
            using (var conn = new NpgsqlConnection(_yoda_pp_connString))
            {
                string sql_string = "INSERT INTO pp.not_registered (sd_sid, title, short_sponsor_name, short_protocol_id)";
                sql_string += "VALUES ('" + pp_id + "', '" + title + "', '" + short_sponsor_name + "', '" + protid + "');";
                conn.Execute(sql_string);
            }
        }

    }
}
