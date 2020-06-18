using PostgreSQLCopyHelper;

namespace DataDownloader
{
    public static class LoggingCopyHelper
    {
        public static PostgreSQLCopyHelper<StudyFileRecord> file_record_copyhelper =
            new PostgreSQLCopyHelper<StudyFileRecord>("sf", "source_data_studies")
                .MapInteger("source_id", x => x.source_id)
                .MapVarchar("sd_id", x => x.sd_id)
                .MapVarchar("remote_url", x => x.remote_url)
                .MapInteger("last_sf_id", x => x.last_sf_id)
                .MapTimeStampTz("last_revised", x => x.last_revised)
                .MapBoolean("assume_complete", x => x.assume_complete)
                .MapInteger("download_status", x => x.download_status)
                .MapTimeStampTz("download_datetime", x => x.download_datetime)
                .MapVarchar("local_path", x => x.local_path);

    }

}
