# DataDownloader
Downloads data from mdr data sources to local files, stored as XML.

The functioning of the mdr begins with the creation of a local copy of all the source data. A folder is set up to receive the data, one per source, and the data download process adds files to that folder. The download events are self contained and can take place independently of any further processing. The local copy of the source data simply grows with successive download events. At any point in time the folder holds *all* the data relevant to the mdr from its source, but because the basic details of each file, including the date and time of its download, are recorded later processing stages can select subsets of files from that data store. Sources are trial registries and data repositories, and the download mechanisms used include
* Downloading XML files directly from a source's API, (e.g. for ClicalTrials.gov, PubMed)
* Scraping web pages and generating the XML files from the data obtained (e.g. for ISRCTN, EUCTR, Yoda, BioLincc)
* Downloading CSV files and converting the data into XML files (e.g. for WHO ICTRP data).
The format of the XML files created vary from source to source but represent the initial stage in the process of converting the source data into a consistent schema.<br/><br/>
The program represents the first stage in the 4 stage MDR extraction process:<br/>
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;**Download** => Harvest => Import => Aggregation<br/><br/>
For a much more detailed explanation of the extraction process,and the MDR system as a whole, please see the project wiki (landing page at https://ecrin-mdr.online/index.php/Project_Overview).<br/>

### Parameters
The system is a console app, and takes the following parameters:<br/>
**-s**, followed by an integer: the id of the source to be downloaded, e.g. 100120 = ClinicalTrials.gov.<br/>
**-t**, followed by an integer: the id of the type of fetch (or sometimes search) - see below for more details.<br/>
**-f**, followed by a file path: the path should be that of the source file for those download types that require it.<br/>
**-d**, followed by a date, in the ISO yyyy-MM-dd format: the date should be the cut-off date for those download data types that require one.<br/>
**-q**, followed by an integer: the integer id of a listed query for using within an API<br/>
**-p**, followed by a string of comma delimited integers: the ids of the previous searches that should be used as the basis of this download.<br/>
**-L**: a flag indicating that no logging should take place. Useful in some testing and development scenarios.<br/>

Thus, a parameter string such as<br/>
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;-s 100120 -t 111 -d 2020-09-23<br/>
will cause the system to download files from PubMed that have been revised or added since the 23rd September (the ClinicalTrials.gov API allows this sort of call to be made). The parameters<br/>
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;-s 100135 -t 114 -d 2020-07-14 -q 100003<br/>
would cause the system to download files from PubMed that have been revised since the 14th July and which also contain references to clinical trial registry ids, while the string<br/>
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;-s 100115 -t 113 -f "C:\data\who\update 20200813.csv"<br/>
would cause the system to update the WHO linked data sources with data from the named csv file. The parameter strings:<br/>
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;-s 100126 -t 202 -d 2020-06-12<br/>
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;-s 100126 -t 132 -p 100054<br/>
would first cause the data in ISRCTN that had been added or revised since the 12th of June to be identified (that search having an id of 100054), and then cause that data to be downloaded, as a separate process. The second process does not need to be run immediately after the first.<br/><br/>
A full list of download types and existing query filters is provided at the base of this ReadMe file.<br/>

### Overview
The download process is dependent upon not just the fetch / search type specified and the other parameters but also on the source - the process is highly source dependent.<br/><br/>	
The simplest downloads are those that simply take pre-existing XML files using API calls. This is the case for both ClinicalTrials.gov and PubMed. The files are not transformed in any way at this stage - simply downloaded and stored for later harvesting. Too many API calls in quick succession can lead to access being blocked (the hosts suspect a denial of service attack) so the download process is automatically paused at intervals. This greatly increases the time required but is required to prrevent blocked calls.<br/>	
<br/>	
A group of sources require active web scraping, usually starting from a search page, going to a details page for the main data for each study, and then often to further pages with specific details. Again, the scraping process usually needs to be "throttled down" with the inclusion of automatic pauses. The data is selected and processed to start its conversion to the ECRIN metadata standard form. The XML files that are created and stored by the scraping process are therefore usually straightforward to harvest into the database, as much of the preparatory work ihas been done during this download phase.<br/>	
<br/>	
The WHO ICTRP dataset is available as csv files, with each row corresponding to a study. The downlkoad process here consists of downloading the file (usuallly as weekly updates) and then running through the data to generate the XML files. Again the XML can be structured to partly match the ECRIN metadata schema, making later processing easier. In this case the XML files are distributed to different folders according to the ultimate (trial registry) source of the data, so that the mdr sees each registry as a separate source with a separate database. This is partly to help manage a large dataset, but mostly to more easily substitue the WHO data (which is relatively sparse and often requires extensive cleaning) with richer data scraped directly from the source registry.<br/>	
<br/>	
For large data sources the strategy is generally to use incremental downloads, on a weekly or even nightly basis, to keep the store of XML files as up to date as possible. For smaller sources it is possible and simpler to re-download the whole source. Unfortunately not all large sources havve easy mechanisms to identify new or revised data. The EUCTR for example does not publicly display this date, and is also a difficult web site to scrape (attempts are frequently blocked even with large gaps between scraping calls). TRo save re-scraping all the data each time, which can take a few days, the assumption is made that if a study meets certain criteria - e.g. is marked as 'completed' and has a completed results section - it is unlikely to change further. That is why one of the download options is to re-download only those files classified as 'assumed not yet completed'. The exact criteria for thios status would be source dependent.<br/>	
<br/>	
Even when incremental updating is relatively straightforward, however, the intention is to do a 100% download at least once a year, to ensure that the basic raw material from each registry is regularly 'rebased' to a valid state.

### Logging
Logging of data dowwnload is critical because it provides the basis for orchestrating processes later on in the extraction pathway. A record is created for each study that is downloaded (in study based sources like trial registries) or for each data object downloaded (for object based resources like PubMed). The **'data source record'** that is established includes:
* the source id, 
* the object's own id, in the source data (e.g. a registry identifier or PubMed id), 
* the URL of its record on the web - if it has one. This applies even to data that is not collected directly from the web, such as from WHO csv files. 
* the local path where the XML file downloaded or created is stored
* the datetime that the record was last revised, if available
* a boolean indicating if the record is assumed complete (used when no revision date is available)
* the download status - an integer - where 0 indicates found in a search but not yet (re)downloaded, and 2 indicates downloaded.
* the id of the fetch / search event in which it was last downloaded / created
* the date-time of that fetch / search
* the id of the harvest event in which it was last harvested
* the date-time of that harvest
* the id of the import event in which it was last imported
* the date-time of that import

In other words the 'source record' provides, for each individual downloaded entity, a record of their current status in the system.<br/>	
During a fetch / save event new studies (or objects for PubMed) will generate new records in this table. Existing records will update the records - possibly updating the date last revised as well as the date-time the data was last fetched and the id of the fetch event. The date-time the data was last fetched is later incorporated into the data's "provenance string", even if the data proves to have been unchanged since the previous download. This date-time is also used during harvesting, when an option is to only harvest data that has been downloaded since the last import into the system (N.B. not since the last harvest).<br/>	 
<br/>
A summary record is also provided for each download and stored in the saf_events table in the monitor database (saf = search and fetch). This allows the system to interrogate the nature of the last download

### Provenance
* Author: Steve Canham
* Organisation: ECRIN (https://ecrin.org)
* System: Clinical Research Metadata Repository (MDR)
* Project: EOSC Life
* Funding: EU H2020 programme, grant 824087


### Download Types
The range of parameters illustrate the need for the variety of approaches required to deal with the various types of source material. The types of download available, together with the three digit integer id of each, are:<br/>	<br/>	
101:	All records (download)<br/>	
*Identifies and downloads XML files, one per record, from the entire data source. All available files are downloaded.*	

102:	All records (scrape)<br/>	
*Scrapes data, creates XML files, one per record, from the entire data source. All available records are processed.*	

103:	All records (file)<br/>	
*A local file (e.g. CSV file downloaded from WHO) used as the data source. Records transformed into local XML files. Requires file path*

111:	All new or revised records (download)	<br/>
*Identifies and downloads XML files, one per each record that has been added new or revised since the given cutoff date, within the entire data source.	Requires cut off date*

112:	All new or revised records (scrape)<br/>	
*Scrapes data and creates XML files, one per record, from  data that is new or revised since the given cutoff data, inspecting the entire data source. Requires cut off date*

113:	All new or revised records (file)<br/>	
*Uses a downloaded file, with new and / or revised data since the last data fetch (usually as provided by the source). Local XML files created or amended. Requires file path*

114:	All new or revised records with a filter applied (download)<br/>	
*Identifies and downloads XML files, one per each record that has been added new or revised since the given cutoff date, within a filtered source record set.	Requires cut off date, query id*

121:	Filtered records (download)	<br/>
*Identifies and downloads XML files, one per record, that meet specific search criteria (excluding revision date). The search type should be identified.	Requires query id

122:	Filtered records (scrape)	
*Scrapes data and creates XML files, one per record, from data that meets specific search criteria (excluding revision date). The search type should be identified.	Requires query id*

123:	Filtered records (file)	
*Uses a downloaded file, containing data that meets specific search criteria (excluding those based on revision date). Local XML files created or amended. The search type should be identified.	Requires file path, query id*

131:	Records from prior search (download)<br/>	
*Downloads XML files, one per record, that have been previously identified by a search as requiring download. The search(es) should be identified.	Requires previous search id(s)*

132:	Records from prior search (scrape)<br/>	
*Scrapes data, and creates or amends XML files, one per record, that have been previously identified by a search as requiring download. The search(es) should be identified.	Requires previous search id(s)*

133:	Records from prior search (file)<br/>	
*Uses a downloaded file and extracts from it data that has previously been identified as required by a search. The search(es) should be identified.	requires file path, previous search id(s)*

134:	Records from prior search and new or revised records (download)	<br/>
*Downloads XML files, one per record, that have been previously identified by a search as requiring download, and / or that have been revised or added since the cutoff date. The search(es) should be identified.	requires cut off date, previous search id(s)*

141:	Assumed incomplete records (download)	<br/>
*Identifies and downloads XML files, one per each record, that are assumed to be incomplete (using source specific criteria), within the entire data source.*

142:	Assumed incomplete records (scrape)	<br/>
*Scrapes data and creates XML files, one per record, from  data that is assumed to be incomplete (using source-specific criteria), inspecting the entire data source.*

143:	Assumed incomplete records (file)	<br/>
*Uses a downloaded file, with data that is assumed to be incomplete (using source specific criteria). Local XML files created or amended.	Requires file path*

201:	Full search (records located only)<br/>	
*Identifies data or web page, including its source URL, across the entire data source, for later fetch.*	

202:	New or revised records (records located only)<br/>	
*Identifies data or web page, including its source URL, that meet the criteria of having been revised or added since the given cutoff date. For later fetch. Requires cut off date*

203:	Filtered search (records located only)<br/>	
*Identifies data or web page, including its source URL, that meet the criteria of a focused search, for later fetch. The search type should be specified.	Requires query id*

204:	New or revised records and filtered search  (records located only)	
*Identifies data or web page, including its source URL, that meet the criteria of a focused search, AND which have also been revised or added since the given cutoff date, for later fetch.	Requires cut off date, query id*

205:	Search using MDR data (records located only)<br/>	
*Identifies data or web pages, including their source URLs, where previously processed MDR data indicates it should be fetched, e.g. references in one source to another.	Requires query id*

### Query types
The types of wquery are likely to grow with time as different sources are used. At the moment the only use for these filters is with PubMed data. The current filter queries used are:

10003	PubMed Registries: PubMed abstracts with references to any trial registry
*Looks in PubMed with entries for any 'DataBank' that corresponds to a trial registry - loops through each registry in turn*

10004	Pubmed-Study References	: Identifies PubMed references in Study sources that have not yet been downloaded
*Carried out entirely within the system databases*

