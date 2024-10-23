use [master]
go 

-- -- -- Alter #RebuildDatabase to 1 to drop and recreate the database, or 0 to keep the existing.
declare 
  @RebuildDatabase bit = 0;

if (@RebuildDatabase = 1)
begin
    print '*** Dropping database OperationsLocal';
    drop database [OperationsLocal];
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
        ) 
    begin 
      exec [OperationsLocal].[dbo].[sp_fulltext_database] @action = 'enable'
    end
    alter database [OperationsLocal]
    set ansi_null_default off
    alter database [OperationsLocal]
    set ansi_nulls off
    alter database [OperationsLocal]
    set ansi_padding off
    alter database [OperationsLocal]
    set ansi_warnings off
    alter database [OperationsLocal]
    set arithabort off
    alter database [OperationsLocal]
    set auto_close off
    alter database [OperationsLocal]
    set auto_shrink off
    alter database [OperationsLocal]
    set auto_update_statistics on
    alter database [OperationsLocal]
    set cursor_close_on_commit off
    alter database [OperationsLocal]
    set cursor_default global
    alter database [OperationsLocal]
    set concat_null_yields_null off
    alter database [OperationsLocal]
    set numeric_roundabort off
    alter database [OperationsLocal]
    set quoted_identifier off
    alter database [OperationsLocal]
    set recursive_triggers off
    alter database [OperationsLocal]
    set disable_broker
    alter database [OperationsLocal]
    set auto_update_statistics_async off
    alter database [OperationsLocal]
    set date_correlation_optimization off
    alter database [OperationsLocal]
    set trustworthy off
    alter database [OperationsLocal]
    set allow_snapshot_isolation off
    alter database [OperationsLocal]
    set parameterization simple
    alter database [OperationsLocal]
    set read_committed_snapshot off
    alter database [OperationsLocal]
    set honor_broker_priority off
    alter database [OperationsLocal]
    set recovery full
    alter database [OperationsLocal]
    set multi_user
    alter database [OperationsLocal]
    set page_verify checksum
    alter database [OperationsLocal]
    set db_chaining off
    alter database [OperationsLocal]
    set filestream(non_transacted_access = off)
    alter database [OperationsLocal]
    set target_recovery_time = 60 seconds
    alter database [OperationsLocal]
    set delayed_durability = disabled
    alter database [OperationsLocal]
    set accelerated_database_recovery = off
    use [master]
    alter database [OperationsLocal]
    set query_store = off
    alter database [OperationsLocal]
    set read_write
end;
print '*** Switching to OperationsLocal database';
use OperationsLocal;
go
if not exists (select *
from sys.schemas
where name = N'rpt')
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
        where TABLE_NAME = 'OperationFlowStates'
            and TABLE_SCHEMA = 'rpt'
    ) drop table [rpt].OperationFlowStates;
	go
if exists(
        select *
        from INFORMATION_SCHEMA.TABLES
        where TABLE_NAME = 'OperationFlowStateHistory'
            and TABLE_SCHEMA = 'rpt'
    ) drop table [rpt].OperationFlowStateHistory;
go
 -- create FlowStates table to define friendly names of allowed states
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
go
-- populate FlowStates table
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
-- create OperationFlowStates table to house runtime flow states
print '*** Creating OperationFlowStates table';
go
create table [rpt].[OperationFlowStates](
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
    [ProcessId] [nvarchar](100) NULL,
    [SequenceNumber] [int] NULL,
    constraint [PK_rpt.ReportFlowStates] primary key clustered ([OperationFlowStateID] asc) with (
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
go -- create history table
    print '*** Creating OperationFlowStatesHistory table';
	go
create table [rpt].[OperationFlowStateHistory](
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
    [ProcessId] [nvarchar](100) null,
    [SequenceNumber] [int] null,
    constraint [PK_rpt.OperationFlowStateHistory] primary key clustered ([OperationFlowStateHistoryID] asc) with (
        pad_index = off,
        statistics_norecompute = off,
        ignore_dup_key = off,
        allow_row_locks = on,
        allow_page_locks = on,
        optimize_for_sequential_key = off
    ) on [primary]
) on [primary] textimage_on [primary];
go 
print '*** Ensuring Read and Write stored procedures';
go 
create or alter procedure [rpt].[OperationFlowState_Read] @UniqueKey nvarchar(100) as 
begin
set nocount on;
select (
        select UniqueKey,
            OperationName,
            ActivityName,
            ActivityState,
            TimeStarted,
            TimeEnded,
            TimeUpdated,
            Trace,
            Reason,
            ProcessId,
            [RetryCount],
            SequenceNumber
        from rpt.OperationFlowStates
        where UniqueKey = @UniqueKey for json path,
            without_array_wrapper
    ) as JsonResult;
end;
go 
create or alter procedure [rpt].[OperationFlowState_Write] @json nvarchar(max) as 
begin
set nocount on;
declare @Timestamp datetime2 = GetUtcDate();
merge rpt.OperationFlowStates as target using (
    select json_value(@json, '$.UniqueKey') as UniqueKey,
        json_value(@json, '$.OperationName') as OperationName,
        json_value(@json, '$.ActivityName') as ActivityName,
        json_value(@json, '$.TimeStarted') as TimeStarted,
        json_value(@json, '$.TimeEnded') as TimeEnded,
        json_value(@json, '$.ActivityState') as ActivityState,
        json_value(@json, '$.ActivityStateName') as ActivityStateName,
        json_value(@json, '$.Trace') as Trace,
        json_value(@json, '$.Reason') as Reason,
        json_value(@json, '$.ProcessId') as ProcessId,
        json_value(@json, '$.RetryCount') as [RetryCount],
        json_value(@json, '$.SequenceNumber') as SequenceNumber,
        @Timestamp as TimeUpdated
) as source on (target.UniqueKey = source.UniqueKey)
when matched then
update
set OperationName = source.OperationName,
    ActivityName = source.ActivityName,
    TimeEnded = source.TimeEnded,
    ActivityState = source.ActivityState,
    ActivityStateName = source.ActivityStateName,
    Trace = source.Trace,
    Reason = source.Reason,
    ProcessId = source.ProcessId,
    SequenceNumber = source.SequenceNumber,
    [RetryCount] = source.[RetryCount],
    TimeUpdated = source.TimeUpdated -- we only save the start time in when the record is first made.
    when not matched then
insert (
        UniqueKey,
        OperationName,
        ActivityName,
        TimeStarted,
        TimeEnded,
        ActivityState,
        ActivityStateName,
        Trace,
        Reason,
        ProcessId,
        SequenceNumber,
        TimeUpdated,
        [RetryCount]
    )
values (
        source.UniqueKey,
        source.OperationName,
        source.ActivityName,
        @Timestamp,
        source.TimeEnded,
        source.ActivityState,
        source.ActivityStateName,
        source.Trace,
        source.Reason,
        source.ProcessId,
        source.SequenceNumber,
        source.TimeUpdated,
        source.[RetryCount]
    );
;
insert into rpt.OperationFlowStateHistory (
        UniqueKey,
        OperationName,
        ActivityName,
        TimeStarted,
        TimeEnded,
        ActivityState,
        ActivityStateName,
        Trace,
        Reason,
        ProcessId,
        [RetryCount],
        SequenceNumber,
        TimeUpdated
    )
values (
        json_value(@json, '$.UniqueKey'),
        json_value(@json, '$.OperationName'),
        json_value(@json, '$.ActivityName'),
        json_value(@json, '$.TimeStarted'),
        json_value(@json, '$.TimeEnded'),
        json_value(@json, '$.ActivityState'),
        json_value(@json, '$.ActivityStateName'),
        json_value(@json, '$.Trace'),
        json_value(@json, '$.Reason'),
        json_value(@json, '$.ProcessId'),
        json_value(@json, '$.RetryCount'),
        json_value(@json, '$.SequenceNumber'),
        cast(@Timestamp as DateTime2)
    )
if (json_value(@json, '$.ActivityState') > 8)
begin
   declare @maxRetries int = (
      select max(RetryCount) 
      from rpt.OperationFlowStateHistory 
      where UniqueKey = json_value(@json, '$.UniqueKey')
   );
update rpt.OperationFlowStates 
set RetryCount = @maxRetries 
where UniqueKey = json_value(@json, '$.UniqueKey')
end
end;
go 
print '*** Ensuring Purge stored procedures';
go
create or alter procedure [rpt].[OperationFlowState_Purge] as 
begin
set nocount on;
delete from rpt.OperationFlowStates
where ActivityState = 9 or ActivityState = 10;
-- rebuild any indexes
alter index all on rpt.operationFlowStates rebuild;
end;
go 
Create or alter procedure [rpt].[OperationFlowStateHistory_Purge] as 
begin
set nocount on;
truncate table rpt.OperationFlowStateHistory;
end;
go 
print '*** Done.';