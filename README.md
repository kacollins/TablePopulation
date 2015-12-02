# TablePopulation
Generates insert scripts.

The goal is create the scripts to insert data into lookup tables for deployment.

Steps:
* Read list of tables from file
    * Schema name
    * Table name
    * Identity flag
* Get the data in each table
* Generate the insert script for each record, checking whether the ID already exists
    * Assume that the first column is the primary key
    * If the table has an identity, set identity_insert on and off
* Output the scripts to one sql file per table
