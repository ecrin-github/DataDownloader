# DataDownloader
Downloads data from sources to local files, OR in some cases identifies files / pages for later downloading of the full data.<br/>
Sources are trial registries and data repositories.

The mechanisms used include
* Downloading XML files directly from a source's API, (e.g. for ClicalTrials.gov, PubMed)
* Scraping web pages and generating the XML files (e.g. for ISRCTN, EUCTR, Yoda, BioLincc)
* Downloading CSV files and converrting the data into XML files.

Download types include<br/>
10	Full initial fetch (download)<br/>
Identifies and downloads XML files, one per record, from the entire data source.

11	Full initial fetch (scrape)	<br/>
Scrapes data, creates XML files, one per record, from the entire data source.

12	Full update (download)<br/>	
Identifies and downloads XML files, one per record, from new or revised records within the entire data source.

13	Full update (scrape)<br/>
Scrapes data, creates XML files, one per record, from new or revised data within the entire data source.

14	Focused fetch (download)<br/>
Identifies and downloads XML files, one per record, that meet the criteria of a focused search.

15	Focused fetch (scrape)<br/>
Scrapes data, creates XML files, one per record, that meet the criteria of a focused search.

16	Focused update (download)<br/>
Identifies and downloads XML files, one per record, from new or revised records that also meet the criteria of a focused search.

17	Focused update (scrape)<br/>
Scrapes data, creates XML files, one per record, from new or revised data that also meet the criteria of a focused search.

18	Full search<br/>
Identifies data or web page, including its URL, across the entire data source, for later examination and possible fetch

19	Focused search<br/>
Identifies data or web page, including its URL, that meet the criteria of a focused search, for later examination and possible fetch

20	Full initial fetch (file)<br/>
Downloads file (e.g. CSV file from WHO) as the precursor for processing. Stored directly in DB rather than as XML files.

21	Full update (file)<br/>
Downloads file with new and / or revised data (e.g. CSV file from WHO). Stored directly in DB rather than as XML files.

The program first identifies the available studies by 'searching' (with no filters applied) to bring back a list of the studies in the system, including the links to the details pages for each. It then visits each of those pages and scrapes the available details about the study. 

When a study has multiple details pages, as many studies do because they are run in more than one country, (and a page exists for each country), then only the first of those details pages is used.

### Provenance
* Author: Steve Canham
* Organisation: ECRIN (https://ecrin.org)
* System: Clinical Research Metadata Repository (MDR)
* Project: EOSC Life
* Funding: EU H2020 programme, grant 824087

Replaces BioLINCCScrape and YodaFetch, for now. More to follow
