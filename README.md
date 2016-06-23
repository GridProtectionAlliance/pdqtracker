![PDQ Tracker](https://raw.githubusercontent.com/GridProtectionAlliance/pdqtracker/master/Readme%20files/Logo.png)

# PDQ Tracker

The PDQ Tracker administered by the Grid Protection Alliance (GPA) is a high-performance, real-time data processing engine designed to raise alarms, track states, store statistics, and generate reports on both the availability and accuracy of streaming synchrophasor data. PDQ Tracker will work with any vendor’s PDC and synchrophasor data infrastructure; measure and automatically produce periodic reports on phasor data completeness and correctness; alarm and/or create emails in real-time if data quality problems are detected.

The purpose of PDQ Tracker is (1) to measure phasor data quality, (2) to disseminate data quality information to assure data quality awareness and facilitate data quality problem resolution, and (3) to provide a platform that can be extended to provide comprehensive data quality analytics including phasor data correction. See: [PDQTracker High Level Requirements](http://www.gridprotectionalliance.org/docs/products/PDQTracker/highlevelrequirements.pdf).

![PDQTracker Overview](https://raw.githubusercontent.com/GridProtectionAlliance/pdqtracker/master/Readme%20files/Overview.png)

PDQ Tracker is being developed with sponsorship from Dominion Virginia Power and Peak RC.

# Documentation and Support

* Documentation for PDQTracker can be found [here](https://github.com/GridProtectionAlliance/pdqtracker/tree/master/Source/Documentation).
* Get in contact with our development team on our new [discussion boards](http://discussions.gridprotectionalliance.org/c/gpa-products/pdqtracker).
* View old discussion board topics [here](http://pdqtracker.codeplex.com/discussions).
* Check out the [pdqtracker wiki](https://gridprotectionalliance.org/wiki/doku.php?id=pdqtracker:overview).

# Deployment

1. Make sure your system meets all the [requirements](#requirements) below.
* Choose a [download](#downloads) below.
* Unzip.
* Run "Setup.exe".
* Follow the wizard.
* Enjoy.

## Requirements

* 64-bit Windows 7 or newer.
* .NET 4.6 or newer.
* Database Management System such as:
  * SQL Server (Recommended)
  * MySQL
  * Oracle
  * PostgreSQL
  * SQLite (Not recommended for production use) - included

## Downloads

* Download the release candidate [here](https://github.com/GridProtectionAlliance/pdqtracker/releases).
* Download the nightly build [here](http://www.gridprotectionalliance.org/nightlybuilds/PDQTracker/Beta/PDQTracker.Installs.zip).

# Contributing
If you would like to contribute please:

1. Read our [styleguide](https://www.gridprotectionalliance.org/docs/GPA_Coding_Guidelines_2011_03.pdf).
* Fork the repository.
* Code awesomeness.
* Create a pull request.
 
# License
PDQTracker is licensed under the [MIT License](https://gridprotectionalliance.org/wiki/doku.php?id=pdqtracker:overview).
