
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
declare @RebuildDatabase bit = 0;
if (@RebuildDatabase = 1) begin print '*** Dropping database OperationsLocal';
drop database [OperationsLocal];
-------------------------------------------------------------------------------------------------------
print '*** Creating database OperationsLocal';
create database [OperationsLocal] containment = none on primary (
  name = N'OperationsLocal',
  filename = N'/var/opt/mssql/data/OperationsLocal.mdf',
  size = 8192kb,
  maxsize = UNLIMITED,
  filegrowth = 65536kb
) log on (
  name = N'OperationsLocal_log',
  filename = N'/var/opt/mssql/data/OperationsLocal_log.ldf',
  size = 139264kb,
  maxsize = 2048gb,
  filegrowth = 65536kb
) with catalog_collation = database_default;
if (
  1 = fulltextserviceproperty('IsFullTextInstalled')
) begin exec [OperationsLocal].[dbo].[sp_fulltext_database] @action = 'enable'
end alter database [OperationsLocal]
set ansi_null_default off alter database [OperationsLocal]
set ansi_nulls off alter database [OperationsLocal]
set ansi_padding off alter database [OperationsLocal]
set ansi_warnings off alter database [OperationsLocal]
set arithabort off alter database [OperationsLocal]
set auto_close off alter database [OperationsLocal]
set auto_shrink off alter database [OperationsLocal]
set auto_update_statistics on alter database [OperationsLocal]
set cursor_close_on_commit off alter database [OperationsLocal]
set cursor_default global alter database [OperationsLocal]
set concat_null_yields_null off alter database [OperationsLocal]
set numeric_roundabort off alter database [OperationsLocal]
set quoted_identifier off alter database [OperationsLocal]
set recursive_triggers off alter database [OperationsLocal]
set disable_broker alter database [OperationsLocal]
set auto_update_statistics_async off alter database [OperationsLocal]
set date_correlation_optimization off alter database [OperationsLocal]
set trustworthy off alter database [OperationsLocal]
set allow_snapshot_isolation off alter database [OperationsLocal]
set parameterization simple alter database [OperationsLocal]
set read_committed_snapshot off alter database [OperationsLocal]
set honor_broker_priority off alter database [OperationsLocal]
set recovery full alter database [OperationsLocal]
set multi_user alter database [OperationsLocal]
set page_verify checksum alter database [OperationsLocal]
set db_chaining off alter database [OperationsLocal]
set filestream(non_transacted_access = off) alter database [OperationsLocal]
set target_recovery_time = 60 seconds alter database [OperationsLocal]
set delayed_durability = disabled alter database [OperationsLocal]
set accelerated_database_recovery = off use [master] alter database [OperationsLocal]
set query_store = off alter database [OperationsLocal]
set read_write
end;
-------------------------------------------------------------------------------------------------------
print '*** Switching to OperationsLocal database';
use OperationsLocal;
go if not exists (
    select *
    from sys.schemas
    where name = N'rpt'
  ) exec ('create schema [rpt] authorization dbo');
go print '*** Dropping existing tables' -- clear existing tables
go if exists(
    select *
    from INFORMATION_SCHEMA.TABLES
    where TABLE_NAME = 'FlowStates'
      and TABLE_SCHEMA = 'rpt'
  ) drop table [rpt].FlowStates;
go if exists(
    select *
    from INFORMATION_SCHEMA.TABLES
    where TABLE_NAME = 'OperationFlowStates'
      and TABLE_SCHEMA = 'rpt'
  ) drop table [rpt].OperationFlowStates;
go if exists(
    select *
    from INFORMATION_SCHEMA.TABLES
    where TABLE_NAME = 'OperationFlowStateHistory'
      and TABLE_SCHEMA = 'rpt'
  ) drop table [rpt].OperationFlowStateHistory;
go if exists(
    select *
    from INFORMATION_SCHEMA.TABLES
    where TABLE_NAME = 'ActivitySettings'
      and TABLE_SCHEMA = 'rpt'
  ) drop table [rpt].ActivitySettings;
-------------------------------------------------------------------------------------------------------
go -- create FlowStates table to define friendly names of allowed states
  print '*** Creating FlowStates table'
go create table [rpt].[FlowStates](
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
go print '*** Inserting predefined flow states';
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
-- create OperationFlowStates table to house runtime flow states
print '*** Creating OperationFlowStates table';
go create table [rpt].[OperationFlowStates](
    [OperationFlowStateID] [bigint] identity(1, 1) NOT NULL,
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
    [HostServer] nvarchar(100) null constraint [PK_rpt.ReportFlowStates] primary key clustered ([OperationFlowStateID] asc) with (
      pad_index = off,
      statistics_norecompute = off,
      ignore_dup_key = off,
      allow_row_locks = on,
      allow_page_locks = on,
      optimize_for_sequential_key = off
    ) on [primary]
  ) on [primary] textimage_on [primary];
go
alter table [rpt].[OperationFlowStates]
add constraint [DF_ReportFlowStates_ActivityState] default ((0)) for [ActivityState];
-------------------------------------------------------------------------------------------------------
go -- create history table
  print '*** Creating OperationFlowStatesHistory table';
go create table [rpt].[OperationFlowStateHistory](
    [OperationFlowStateHistoryID] [bigint] identity(1, 1) not null,
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
    [HostServer] nvarchar(100) null constraint [PK_rpt.OperationFlowStateHistory] primary key clustered ([OperationFlowStateHistoryID] asc) with (
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
-- create settings table
set ansi_nulls on
go
set quoted_identifier on
go create table [rpt].[ActivitySettings](
    [ActivitySettingsId] [int] identity(1, 1) not null,
    [ActivityName] [nvarchar](100) not null,
    [NumberOfRetries] [int] null,
    [InitialDelay] [float] null,
    [BackOffCoefficient] [float] null,
    [MaximumDelay] [float] null,
    [RetryTimeout] [float] null,
    [ActivityTimeout] [float] null,
    [LoadFactor] float null,
    [MaximumDelayCount] int null,
    [PartitionId] [int] null,
    constraint [PK_ActivitySettings] primary key clustered ([ActivitySettingsId] asc) with (
      pad_index = off,
      statistics_norecompute = off,
      ignore_dup_key = off,
      allow_row_locks = on,
      allow_page_locks = on,
      optimize_for_sequential_key = off
    ) on [primary]
  ) on [primary]
go
alter table [rpt].[ActivitySettings]
add constraint [DF_ActivitySettings_PartitionId] default ((0)) for [PartitionId]
go -- add test data
  use [OperationsLocal]
go
set identity_insert [rpt].[ActivitySettings] on
go
insert [rpt].[ActivitySettings] (
    [ActivitySettingsId],
    [ActivityName],
    [NumberOfRetries],
    [InitialDelay],
    [BackOffCoefficient],
    [MaximumDelay],
    [RetryTimeout],
    [ActivityTimeout],
    [LoadFactor],
    [MaximumDelayCount],
    [PartitionId]
  )
values (
    1,
    N'Default',
    8,
    0.1,
    1.4142,
    2.5,
    24,
    1,
    0.0,
    8,
    0
  )
go
insert [rpt].[ActivitySettings] (
    [ActivitySettingsId],
    [ActivityName],
    [NumberOfRetries],
    [InitialDelay],
    [BackOffCoefficient],
    [MaximumDelay],
    [RetryTimeout],
    [ActivityTimeout],
    [LoadFactor],
    [MaximumDelayCount],
    [PartitionId]
  )
values (
    2,
    N'Test',
    3,
    0.03,
    1,
    null,
    0.2,
    0.03,
    0.0,
    null,
    0
  )
go
insert [rpt].[ActivitySettings] (
    [ActivitySettingsId],
    [ActivityName],
    [NumberOfRetries],
    [InitialDelay],
    [BackOffCoefficient],
    [MaximumDelay],
    [RetryTimeout],
    [ActivityTimeout],
    [LoadFactor],
    [MaximumDelayCount],
    [PartitionId]
  )
values (
    3,
    N'ActivityAlpha',
    3,
    0.03,
    null,
    null,
    0.2,
    0.03,
    0.0,
    null,
    0
  )
go
insert [rpt].[ActivitySettings] (
    [ActivitySettingsId],
    [ActivityName],
    [NumberOfRetries],
    [InitialDelay],
    [BackOffCoefficient],
    [MaximumDelay],
    [RetryTimeout],
    [ActivityTimeout],
    [LoadFactor],
    [MaximumDelayCount],
    [PartitionId]
  )
values (
    4,
    N'ActivityBravo',
    3,
    0.03,
    null,
    null,
    0.2,
    0.03,
    0.0,
    null,
    0
  )
go
insert [rpt].[ActivitySettings] (
    [ActivitySettingsId],
    [ActivityName],
    [NumberOfRetries],
    [InitialDelay],
    [BackOffCoefficient],
    [MaximumDelay],
    [RetryTimeout],
    [ActivityTimeout],
    [LoadFactor],
    [MaximumDelayCount],
    [PartitionId]
  )
values (
    5,
    N'ActivityCharlie',
    3,
    0.03,
    null,
    null,
    0.2,
    0.03,
    0.0,
    null,
    0
  )
go
insert [rpt].[ActivitySettings] (
    [ActivitySettingsId],
    [ActivityName],
    [NumberOfRetries],
    [InitialDelay],
    [BackOffCoefficient],
    [MaximumDelay],
    [RetryTimeout],
    [ActivityTimeout],
    [LoadFactor],
    [MaximumDelayCount],
    [PartitionId]
  )
values (
    8,
    N'FinishAsync',
    8,
    0.1,
    1.4142,
    null,
    24,
    null,
    0.0,
    null,
    0
  )
insert [rpt].[ActivitySettings] (
    [ActivitySettingsId],
    [ActivityName],
    [NumberOfRetries],
    [InitialDelay],
    [BackOffCoefficient],
    [MaximumDelay],
    [RetryTimeout],
    [ActivityTimeout],
    [LoadFactor],
    [MaximumDelayCount],
    [PartitionId]
  )
values (
    9,
    N'Infra',
    10,
    0.25,
    1,
    0.25,
    24,
    null,
    0.0,
    null,
    0
  )
insert [rpt].[ActivitySettings] (
    [ActivitySettingsId],
    [ActivityName],
    [NumberOfRetries],
    [InitialDelay],
    [BackOffCoefficient],
    [MaximumDelay],
    [RetryTimeout],
    [ActivityTimeout],
    [LoadFactor],
    [MaximumDelayCount],
    [PartitionId]
  )
values (
    10,
    N'InfraTest',
    2,
    0.03,
    1,
    0.25,
    24,
    null,
    0.0,
    null,
    0
  )
insert [rpt].[ActivitySettings] (
    [ActivitySettingsId],
    [ActivityName],
    [NumberOfRetries],
    [InitialDelay],
    [BackOffCoefficient],
    [MaximumDelay],
    [RetryTimeout],
    [ActivityTimeout],
    [LoadFactor],
    [MaximumDelayCount],
    [PartitionId]
  )
values (
    11,
    N'OrchestrationAlpha',
    2,
    0.03,
    1,
    0.25,
    24,
    null,
    null,
    null,
    null
  )
insert [rpt].[ActivitySettings] (
    [ActivitySettingsId],
    [ActivityName],
    [NumberOfRetries],
    [InitialDelay],
    [BackOffCoefficient],
    [MaximumDelay],
    [RetryTimeout],
    [ActivityTimeout],
    [LoadFactor],
    [MaximumDelayCount],
    [PartitionId]
  )
values (
    12,
    N'OrchestrationBravo',
    2,
    0.03,
    1,
    0.25,
    24,
    null,
    null,
    null,
    null
  )
insert [rpt].[ActivitySettings] (
    [ActivitySettingsId],
    [ActivityName],
    [NumberOfRetries],
    [InitialDelay],
    [BackOffCoefficient],
    [MaximumDelay],
    [RetryTimeout],
    [ActivityTimeout],
    [LoadFactor],
    [MaximumDelayCount],
    [PartitionId]
  )
values (
    13,
    N'OrchestrationCharlie',
    2,
    0.03,
    1,
    0.25,
    24,
    null,
    null,
    null,
    null
  )
go
set identity_insert [rpt].[ActivitySettings] off
-------------------------------------------------------------------------------------------------------
print '*** Done.';