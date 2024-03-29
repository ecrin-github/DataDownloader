﻿using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.Extensions.Configuration;
using Npgsql;
using PostgreSQLCopyHelper;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DataDownloader
{
    public class MonitorDataLayer
    {
        private string connString;
        private Source source;
        private string sql_file_select_string;
        private string pubmed_api_key;
        private LoggingHelper _logging_helper;


        /// <summary>
        /// Parameterless constructor is used to automatically build
        /// the connection string, using an appsettings.json file that 
        /// has the relevant credentials (but which is not stored in GitHub).
        /// </summary>
        /// 
        public MonitorDataLayer(LoggingHelper logging_helper)
        {
            _logging_helper = logging_helper;

            IConfigurationRoot settings = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .Build();

            NpgsqlConnectionStringBuilder builder = new NpgsqlConnectionStringBuilder();
            builder.Host = settings["host"];
            builder.Username = settings["user"];
            builder.Password = settings["password"];

            builder.Database = "mon";
            connString = builder.ConnectionString;

            // logfile_startofpath = settings["logfilepath"];

            pubmed_api_key = settings["pubmed_api_key"];

            sql_file_select_string = "select id, source_id, sd_id, remote_url, last_revised, ";
            sql_file_select_string += " assume_complete, download_status, local_path, last_saf_id, last_downloaded, ";
            sql_file_select_string += " last_harvest_id, last_harvested, last_import_id, last_imported ";
        }

        public string PubmedAPIKey => pubmed_api_key;

        public Source FetchSourceParameters(int source_id)
        {
            using (NpgsqlConnection Conn = new NpgsqlConnection(connString))
            {
                source = Conn.Get<Source>(source_id);
                return source;
            }
        }
                

        public DateTime? ObtainLastDownloadDate(int source_id)
        {
            using (NpgsqlConnection Conn = new NpgsqlConnection(connString))
            {
                string sql_string = "select max(time_started) from sf.saf_events ";
                sql_string += " where source_id = " + source_id.ToString(); 
                DateTime last_download_dt = Conn.ExecuteScalar<DateTime>(sql_string);
                if (last_download_dt != null)
                {
                    return last_download_dt.Date;
                }
                else
                {
                    return null;
                }
            }
        }


        public DateTime? ObtainLastDownloadDateWithFilter(int source_id, int filter_id)
        {
            using (NpgsqlConnection Conn = new NpgsqlConnection(connString))
            {
                string sql_string = "select max(time_started) from sf.saf_events ";
                sql_string += " where source_id = " + source_id.ToString();
                sql_string += " and filter_id = " + filter_id.ToString();
                DateTime last_download_dt = Conn.ExecuteScalar<DateTime>(sql_string);
                if (last_download_dt != null)
                {
                    return last_download_dt.Date;
                }
                else
                {
                    return null;
                }
            }
        }


        public SFType FetchTypeParameters(int sftype_id)
        {
            using (NpgsqlConnection Conn = new NpgsqlConnection(connString))
            {
                return Conn.Get<SFType>(sftype_id);
            }
        }


        public int GetNextSearchFetchId()
        {
            using (NpgsqlConnection Conn = new NpgsqlConnection(connString))
            {
                string sql_string = "select max(id) from sf.saf_events ";
                int last_id = Conn.ExecuteScalar<int>(sql_string);
                return last_id + 1;
            }

        }


        public StudyFileRecord FetchStudyFileRecord(string sd_id, int source_id)
        {
            using (NpgsqlConnection Conn = new NpgsqlConnection(connString))
            {
                string sql_string = sql_file_select_string;
                sql_string += " from sf.source_data_studies ";
                sql_string += " where sd_id = '" + sd_id + "' and source_id = " + source_id.ToString();
                return Conn.Query<StudyFileRecord>(sql_string).FirstOrDefault();
            }
        }

        
        public ObjectFileRecord FetchObjectFileRecord(string sd_id, int source_id)
        {
            using (NpgsqlConnection Conn = new NpgsqlConnection(connString))
            {
                string sql_string = sql_file_select_string;
                sql_string += " from sf.source_data_objects ";
                sql_string += " where sd_id = '" + sd_id + "' and source_id = " + source_id.ToString();
                return Conn.Query<ObjectFileRecord>(sql_string).FirstOrDefault();
            }
        }
        

        // used for biolincc only
        public IEnumerable<StudyFileRecord> FetchStudyFileRecords(int source_id)
        {
            string sql_string = "select id, sd_id, local_path ";
            sql_string += " from sf.source_data_studies ";
            sql_string += " where source_id = " + source_id.ToString();
            sql_string += " order by local_path";

            using (NpgsqlConnection Conn = new NpgsqlConnection(connString))
            {
                return Conn.Query<StudyFileRecord>(sql_string);
            }
        }


        public int InsertSAFEventRecord(SAFEvent saf)
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                return (int)conn.Insert<SAFEvent>(saf);
            }
        }


        public bool StoreStudyFileRec(StudyFileRecord file_record)
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                return conn.Update<StudyFileRecord>(file_record);
            }
        }


        public bool StoreObjectFileRec(ObjectFileRecord file_record)
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                return conn.Update<ObjectFileRecord>(file_record);
            }
        }


        public int InsertStudyFileRec(StudyFileRecord file_record)
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                return (int)conn.Insert<StudyFileRecord>(file_record);
            }
        }

        public int InsertObjectFileRec(ObjectFileRecord file_record)
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                return (int)conn.Insert<ObjectFileRecord>(file_record);
            }
        }
        

        public bool UpdateStudyDownloadLog(int source_id, string sd_id, string remote_url,
                         int saf_id, DateTime? last_revised_date, string full_path)
        {
            bool added = false; // indicates if a new record or update of an existing one

            // Get the source data record and modify it
            // or add a new one...
            StudyFileRecord file_record = FetchStudyFileRecord(sd_id, source_id);
            try
            {
                if (file_record == null)
                {
                    // this neeeds to have a new record
                    // check last revised date....???
                    // new record
                    file_record = new StudyFileRecord(source_id, sd_id, remote_url, saf_id,
                                                    last_revised_date, full_path);
                    InsertStudyFileRec(file_record);
                    added = true;
                }
                else
                {
                    // update record
                    file_record.remote_url = remote_url;
                    file_record.last_saf_id = saf_id;
                    file_record.last_revised = last_revised_date;
                    file_record.download_status = 2;
                    file_record.last_downloaded = DateTime.Now;
                    file_record.local_path = full_path;

                    // Update file record
                    StoreStudyFileRec(file_record);
                }

                return added;
            }
            catch(Exception e)
            {
                _logging_helper.LogError("In UpdateStudyDownloadLog: " + e.Message);
                return false;
            }
        }


        public bool UpdateObjectDownloadLog(int source_id, string sd_id, string remote_url,
                         int saf_id, DateTime? last_revised_date, string full_path)
        {
            bool added = false; // indicates if a new record or update of an existing one

            // Get the source data record and modify it
            // or add a new one...
            ObjectFileRecord file_record = FetchObjectFileRecord(sd_id, source_id);

            if (file_record == null)
            {
                // this neeeds to have a new record
                // check last revised date....???
                // new record
                file_record = new ObjectFileRecord(source_id, sd_id, remote_url, saf_id,
                                                last_revised_date, full_path);
                InsertObjectFileRec(file_record);
                added = true;
            }
            else
            {
                // update record
                file_record.remote_url = remote_url;
                file_record.last_saf_id = saf_id;
                file_record.last_revised = last_revised_date;
                file_record.download_status = 2;
                file_record.last_downloaded = DateTime.Now;
                file_record.local_path = full_path;

                // Update file record
                StoreObjectFileRec(file_record);
            }

            return added;
        }

        public bool Downloaded_recently(int source_id, string sd_sid, int days_ago)
        {
            string sql_string = @"select id from sf.source_data_studies
                    where last_downloaded::date >= now()::date - " + days_ago.ToString() + @"
                    and sd_id = '" + sd_sid + @"'
                    and source_id = " + source_id.ToString();

            using (var conn = new NpgsqlConnection(connString))
            {
                return conn.Query<int>(sql_string).FirstOrDefault() > 0 ? true : false;
            }
        }


        public ulong StoreStudyRecs(PostgreSQLCopyHelper<StudyFileRecord> copyHelper, IEnumerable<StudyFileRecord> entities)
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                conn.Open();
                // Returns count of rows written 
                return copyHelper.SaveAll(conn, entities);
            }
        }


        public ulong StoreObjectRecs(PostgreSQLCopyHelper<ObjectFileRecord> copyHelper, IEnumerable<ObjectFileRecord> entities)
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                conn.Open();
                // Returns count of rows written 
                return copyHelper.SaveAll(conn, entities);
            }
        }

    }


    public class MonitorCopyHelper
    {
        public PostgreSQLCopyHelper<StudyFileRecord> file_record_copyhelper =
            new PostgreSQLCopyHelper<StudyFileRecord>("sf", "source_data_studies")
                .MapInteger("source_id", x => x.source_id)
                .MapVarchar("sd_id", x => x.sd_id)
                .MapVarchar("remote_url", x => x.remote_url)
                .MapInteger("last_saf_id", x => x.last_saf_id)
                .MapTimeStampTz("last_revised", x => x.last_revised)
                .MapBoolean("assume_complete", x => x.assume_complete)
                .MapInteger("download_status", x => x.download_status)
                .MapTimeStampTz("last_downloaded", x => x.last_downloaded)
                .MapVarchar("local_path", x => x.local_path);

    }

}

