# Amber Prototype based on Orleans

## Nodejs installation(Linux): 
```
sudo apt-get install curl software-properties-common
curl -sL https://deb.nodesource.com/setup_12.x | sudo bash -
sudo apt-get install nodejs
```

## Nodejs installation(Windows):
Download [nodejs](https://nodejs.org/en/) and install it.

## Frontend installation:
Clone this repo then do the following:
```
cd OrleansExp/TexeraOrleansPrototype/texera/core/new-gui
git checkout henry-orleans-demo
npm install -g @angular/cli
npm install node-sass
npm install
ng build
```

## Amber Requirements:
1. Install dotnet-sdk 3.0
2. Install MySQL and login as admin. Using the following command to create a user with username "orleansbackend" and password "orleans-0519-2019" (this can be changed at Constants.cs)
```
CREATE USER 'orleansbackend'@'%' IDENTIFIED BY 'orleans-0519-2019';
```
3. Create a mysql database called 'orleans' and grant all privileges by using the following commands.
```
CREATE DATABASE orleans;
GRANT ALL PRIVILEGES ON orleans. * TO 'orleansbackend'@'%';
FLUSH PRIVILEGES;
```
4. Run the scripts [MySQL-Main.sql](https://github.com/dotnet/orleans/blob/master/src/AdoNet/Shared/MySQL-Main.sql), [MySQL-Clustering.sql](https://github.com/dotnet/orleans/blob/master/src/AdoNet/Orleans.Clustering.AdoNet/MySQL-Clustering.sql) to create the necessary tables and insert entries in the database.

## Run Amber on your local machine:
### 1.Start MySql Server on local
### 2.Start Silo:
Open terminal and enter:
```
cd OrleansExp/TexeraOrleansPrototype/SiloHost
sudo dotnet run -c Release
```
### 3.Start Client:
Open another terminal and enter:
```
cd TexeraOrleansBackend/TexeraOrleansPrototype/webapi-project
sudo dotnet run
```
### 4.Create workflow through Web GUI
This is a step-by-step guide for creating and runnning Workflow TPC-H W1 in [Amber paper](http://www.vldb.org/pvldb/vol13/p740-kumar.pdf).
Download 1G TPC-H sample dataset from https://drive.google.com/drive/folders/1FC47Kh_xvb8Zact-boFIElKmzz00Ouqx to your local machine.
Go to `http://localhost:7070`, you can see a web GUI for Amber:
![web GUI](http://drive.google.com/uc?export=view&id=15_-lT_asJ6YzePln4tVvvNrGRnoqK7Th)

Drag Source -> Scan operator from left panel and drop it on the canvas:
![Scan](http://drive.google.com/uc?export=view&id=1OJ-MsaK5ISMuyzuWuXjX_W5KpzyVc_yX)

Then, drag and drop Utilities -> Comparison, LocalGroupBy, GlobalGroupBy and Sort -> Sort respectively. They will automatically be linked with the previous operator. Your workflow should look like this:
![W1](http://drive.google.com/uc?export=view&id=1mvv7J6QVYEXHEQKYQRmuGwrn1pBy6mMo)

You can specifiy properties for each operator on the right panel. Each operator should have the following properties:

Scan:

![Scan properties](http://drive.google.com/uc?export=view&id=1qf2q8eEglarhQ1mr3ajF5L6DNHQLtcrX)

Comparison:

![Comparison properties](http://drive.google.com/uc?export=view&id=1lBGkUF4tIyry5zqkQVgvogRgZoHCQgZP)

LocalGroupBy:

![LocalGroupBy properties](http://drive.google.com/uc?export=view&id=1C_EYg2g6S9FT_xFI_Su5CGYWfuXVFgYi)

GlobalGroupBy:

![GlobalGroupBy properties](http://drive.google.com/uc?export=view&id=1YFiRbyXZzszDGM2e8sY3JN8Uzmiha1Ms)

Sort:

![Sort properties](http://drive.google.com/uc?export=view&id=1QzqOalYv4oMBMnx23orl6gMlMtn1MY-v)

Click the "Run" button in upper-right corner to run the workflow. After completion, the following result will pop up from the bottom:

![result](http://drive.google.com/uc?export=view&id=1HG7cnoXKgXdpjYFX4r2DZkuga8JaFP19)

