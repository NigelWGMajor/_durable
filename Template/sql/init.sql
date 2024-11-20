
------------------------------------------------------------------------------------------------------
-- This script generates the main database tables
-- and populates the startup data
------------------------------------------------------------------------------------------------------
use [master]
go 
-------------------------------------------------------------------------------------------------------
--
-- Alter @RebuildDatabase to 1 to drop and recreate the database, or 0 to keep the existing.
--
-------------------------------------------------------------------------------------------------------
declare @RebuildDatabase bit = 1;
if (@RebuildDatabase = 1) 
begin 
-- print '*** Dropping database FlowManagement';
-- drop database [FlowManagement];
-------------------------------------------------------------------------------------------------------
print '*** Creating database FlowManagement';
create database [FlowManagement] containment = none on primary (
  name = N'FlowManagement',
  filename = N'/var/opt/mssql/data/FlowManagement.mdf',
  size = 8192kb,
  maxsize = UNLIMITED,
  filegrowth = 65536kb
) log on (
  name = N'FlowManagement_log',
  filename = N'/var/opt/mssql/data/FlowManagement_log.ldf',
  size = 139264kb,
  maxsize = 2048gb,
  filegrowth = 65536kb
) with catalog_collation = database_default;
if (
  1 = fulltextserviceproperty('IsFullTextInstalled')
) begin exec [FlowManagement].[dbo].[sp_fulltext_database] @action = 'enable'
end alter database [FlowManagement]
set ansi_null_default off alter database [FlowManagement]
set ansi_nulls off alter database [FlowManagement]
set ansi_padding off alter database [FlowManagement]
set ansi_warnings off alter database [FlowManagement]
set arithabort off alter database [FlowManagement]
set auto_close off alter database [FlowManagement]
set auto_shrink off alter database [FlowManagement]
set auto_update_statistics on alter database [FlowManagement]
set cursor_close_on_commit off alter database [FlowManagement]
set cursor_default global alter database [FlowManagement]
set concat_null_yields_null off alter database [FlowManagement]
set numeric_roundabort off alter database [FlowManagement]
set quoted_identifier off alter database [FlowManagement]
set recursive_triggers off alter database [FlowManagement]
set disable_broker alter database [FlowManagement]
set auto_update_statistics_async off alter database [FlowManagement]
set date_correlation_optimization off alter database [FlowManagement]
set trustworthy off alter database [FlowManagement]
set allow_snapshot_isolation off alter database [FlowManagement]
set parameterization simple alter database [FlowManagement]
set read_committed_snapshot off alter database [FlowManagement]
set honor_broker_priority off alter database [FlowManagement]
set recovery full alter database [FlowManagement]
set multi_user alter database [FlowManagement]
set page_verify checksum alter database [FlowManagement]
set db_chaining off alter database [FlowManagement]
set filestream(non_transacted_access = off) alter database [FlowManagement]
set target_recovery_time = 60 seconds alter database [FlowManagement]
set delayed_durability = disabled alter database [FlowManagement]
set accelerated_database_recovery = off use [master] alter database [FlowManagement]
set query_store = off alter database [FlowManagement]
set read_write
end;
-------------------------------------------------------------------------------------------------------
print '*** Switching to FlowManagement database';
use FlowManagement;
go 
if not exists (
    select *
    from sys.schemas
    where name = N'rpt'
  ) 
exec ('create schema [rpt] authorization dbo');
go 
print '*** Dropping existing tables' -- clear existing tables
go 
if exists(
    select *
    from INFORMATION_SCHEMA.TABLES
    where TABLE_NAME = 'FlowStates'
      and TABLE_SCHEMA = 'rpt'
  ) drop table [rpt].FlowStates;
go 
if exists(
    select *
    from INFORMATION_SCHEMA.TABLES
    where TABLE_NAME = 'FlowStatesInFlight'
      and TABLE_SCHEMA = 'rpt'
  ) 
  drop table [rpt].FlowStatesInFlight;
go 
if exists(
    select *
    from INFORMATION_SCHEMA.TABLES
    where TABLE_NAME = 'FlowStatesHistory'
      and TABLE_SCHEMA = 'rpt'
  ) drop table [rpt].FlowStatesHistory;
go 
if exists(
    select *
    from INFORMATION_SCHEMA.TABLES
    where TABLE_NAME = 'FlowActivitySettings'
      and TABLE_SCHEMA = 'rpt'
  ) drop table [rpt].FlowActivitySettings;
go 
if exists(
    select *
    from INFORMATION_SCHEMA.TABLES
    where TABLE_NAME = 'FlowStatesFinal'
      and TABLE_SCHEMA = 'rpt'
  ) 
  drop table [rpt].FlowStatesFinal;
-------------------------------------------------------------------------------------------------------
go -- create FlowStates table to define friendly names of allowed states
  print '*** Creating FlowStates table'
go 
create table [rpt].[FlowStates](
    [FlowStateId] [tinyint] identity(0, 1) not null,
    [FlowStateName] [nvarchar](100) not null,
    constraint [PK_rpt.FlowStates] primary key CLUSTERED ([FlowStateId] asc) with (
      pad_index = off,
      statistics_norecompute = off,
      ignore_dup_key = off,
      allow_row_locks = on,
      allow_page_locks = on,
      optimize_for_sequential_key = off
    ) ON [PRIMARY]
  ) on [primary];
go 
print '*** Inserting predefined flow states';
go -- populate FlowStates table
insert into rpt.FlowStates(FlowStateName)
values ('unknown');
insert into rpt.FlowStates(FlowStateName)
values ('Ready');
insert into rpt.FlowStates(FlowStateName)
values ('Active');
insert into rpt.FlowStates(FlowStateName)
values ('Redundant');
insert into rpt.FlowStates(FlowStateName)
values ('Deferred');
insert into rpt.FlowStates(FlowStateName)
values ('Completed');
insert into rpt.FlowStates(FlowStateName)
values ('Stuck');
insert into rpt.FlowStates(FlowStateName)
values ('Stalled');
insert into rpt.FlowStates(FlowStateName)
values ('Failed');
insert into rpt.FlowStates(FlowStateName)
values ('Successful');
insert into rpt.FlowStates(FlowStateName)
values ('Unsuccessful');
-------------------------------------------------------------------------------------------------------
-- create FlowStatesInFlight table to house runtime flow states
print '*** Creating FlowStatesInFlight table';
go 
create table [rpt].[FlowStatesInFlight](
    [FlowStateId] [bigint] identity(1, 1) NOT NULL,
    [UniqueKey] [nvarchar](100) NOT NULL,
    [OperationName] [nvarchar](100) NULL,
    [ActivityName] [nvarchar](100) NOT NULL,
    [TimeStarted] [datetime2](7) NULL,
    [TimeEnded] [datetime2](7) NULL,
    [TimeUpdated] [datetime2](7) NULL,
    [ActivityState] [tinyint] NULL,
    [ActivityStateName] [nvarchar](100) NULL,
    [RetryCount] [int] NULL,
    [Trace] [nvarchar](max) NULL,
    [Reason] [nvarchar](max) NULL,
    [InstanceId] [nvarchar](500) NULL,
    [SequenceNumber] [int] NULL,
    [Disruptions] nvarchar(max) null,
    [HostServer] nvarchar(100) null,
    [PrevailingLoadFactor] float null
     constraint [PK_rpt.FlowStatesInFlight] primary key clustered ([FlowStateId] asc) with (
      pad_index = off,
      statistics_norecompute = off,
      ignore_dup_key = off,
      allow_row_locks = on,
      allow_page_locks = on,
      optimize_for_sequential_key = off
    ) on [primary]
  ) on [primary] textimage_on [primary];
go
alter table [rpt].[FlowStatesInFlight]
add constraint [DF_ReportFlowStates_ActivityState] default ((0)) for [ActivityState];
-------------------------------------------------------------------------------------------------------
go -- create history table
  print '*** Creating FlowStatesHistory table';
go 
create table [rpt].[FlowStatesHistory](
    [FlowStatesHistoryId] [bigint] identity(1, 1) not null,
    [UniqueKey] [nvarchar](100) not null,
    [OperationName] [nvarchar](100) null,
    [ActivityName] [nvarchar](100) not null,
    [TimeStarted] [datetime2](7) null,
    [TimeEnded] [datetime2](7) null,
    [TimeUpdated] [datetime2](7) null,
    [ActivityState] [tinyint] null,
    [ActivityStateName] [nvarchar](100) null,
    [RetryCount] [int] null,
    [Trace] [nvarchar](max) null,
    [Reason] [nvarchar](max) null,
    [InstanceId] [nvarchar](500) null,
    [SequenceNumber] [int] null,
    [Disruptions] nvarchar(max) null,
    [HostServer] nvarchar(100) null,
    [PrevailingLoadFactor] float null
     constraint [PK_rpt.FlowStatesHistory] primary key clustered ([FlowStatesHistoryId] asc) with (
      pad_index = off,
      statistics_norecompute = off,
      ignore_dup_key = off,
      allow_row_locks = on,
      allow_page_locks = on,
      optimize_for_sequential_key = off
    ) on [primary]
  ) on [primary] textimage_on [primary];
go 
-------------------------------------------------------------------------------------------------------
go -- create final table
  print '*** Creating FlowStatesFinal table';
go 
create table [rpt].[FlowStatesFinal](
    [FlowStatesFinalId] [bigint] identity(1, 1) not null,
    [UniqueKey] [nvarchar](100) not null,
    [OperationName] [nvarchar](100) null,
    [ActivityName] [nvarchar](100) not null,
    [TimeStarted] [datetime2](7) null,
    [TimeEnded] [datetime2](7) null,
    [TimeUpdated] [datetime2](7) null,
    [ActivityState] [tinyint] null,
    [ActivityStateName] [nvarchar](100) null,
    [RetryCount] [int] null,
    [Trace] [nvarchar](max) null,
    [Reason] [nvarchar](max) null,
    [InstanceId] [nvarchar](500) null,
    [SequenceNumber] [int] null,
    [Disruptions] nvarchar(max) null,
    [HostServer] nvarchar(100) null,
    [PrevailingLoadFactor] float null
     constraint [PK_rpt.FlowStatesFinal] primary key clustered ([FlowStatesFinalId] asc) with (
      pad_index = off,
      statistics_norecompute = off,
      ignore_dup_key = off,
      allow_row_locks = on,
      allow_page_locks = on,
      optimize_for_sequential_key = off
    ) on [primary]
  ) on [primary] textimage_on [primary];
go 
-------------------------------------------------------------------------------------------------------
go -- create settings table

set ansi_nulls on
go
set quoted_identifier on
go 
create table [rpt].[FlowActivitySettings](
    [FlowActivitySettingsId] [int] identity(1, 1) not null,
    [ActivityName] [nvarchar](100) not null,
    [ActivityTimeout] [float] null,
    [LoadFactor] float null,
    [MaximumDelayCount] int null,
    [PartitionId] [int] null,
    constraint [PK_FlowActivitySettings] primary key clustered ([FlowActivitySettingsId] asc) with (
      pad_index = off,
      statistics_norecompute = off,
      ignore_dup_key = off,
      allow_row_locks = on,
      allow_page_locks = on,
      optimize_for_sequential_key = off
    ) on [primary]
  ) on [primary]
go
alter table [rpt].[FlowActivitySettings]
add constraint [DF_FlowActivitySettings_PartitionId] default ((0)) for [PartitionId]
go -- add test data
  use [FlowManagement]
go
set identity_insert [rpt].[FlowActivitySettings] on
go
insert [rpt].[FlowActivitySettings] (
    [FlowActivitySettingsId],
    [ActivityName],
    [ActivityTimeout],
    [LoadFactor],
    [MaximumDelayCount],
    [PartitionId]
  )
values (
    1,
    N'Default',
    1,
    0.01,
    8,
    0
  )
go
insert [rpt].[FlowActivitySettings] (
    [FlowActivitySettingsId],
    [ActivityName],
    [ActivityTimeout],
    [LoadFactor],
    [MaximumDelayCount],
    [PartitionId]
  )
values (
    2,
    N'Test',
    0.03,
    0.01,
    null,
    0
  )
go
insert [rpt].[FlowActivitySettings] (
    [FlowActivitySettingsId],
    [ActivityName],
    [ActivityTimeout],
    [LoadFactor],
    [MaximumDelayCount],
    [PartitionId]
  )
values (
    3,
    N'ActivityAlpha',
    0.03,
    0.2,
    null,
    0
  )
go
insert [rpt].[FlowActivitySettings] (
    [FlowActivitySettingsId],
    [ActivityName],
    [ActivityTimeout],
    [LoadFactor],
    [MaximumDelayCount],
    [PartitionId]
  )
values (
    4,
    N'ActivityBravo',
    0.03,
    0.3,
    null,
    0
  )
go
insert [rpt].[FlowActivitySettings] (
    [FlowActivitySettingsId],
    [ActivityName],
    [ActivityTimeout],
    [LoadFactor],
    [MaximumDelayCount],
    [PartitionId]
  )
values (
    5,
    N'ActivityCharlie',
    0.03,
    0.4,
    null,
    0
  )
go
set identity_insert [rpt].[FlowActivitySettings] off
-------------------------------------------------------------------------------------------------------
print '*** Done.';
