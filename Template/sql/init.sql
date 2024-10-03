use [master]
go
create database [OperationsLocal] 
 containment = none
 on primary 
( name = N'OperationsLocal', filename = N'/var/opt/mssql/data/OperationsLocal.mdf' , size = 8192kb , maxsize = UNLIMITED, filegrowth = 65536kb )
 log on 
( name = N'OperationsLocal_log', filename = N'/var/opt/mssql/data/OperationsLocal_log.ldf' , size = 139264kb , maxsize = 2048gb , filegrowth = 65536kb )
 with catalog_collation = database_default
go
if (1 = fulltextserviceproperty('IsFullTextInstalled'))
begin
exec[OperationsLocal].[dbo].[sp_fulltext_database] @action = 'enable'
end
go
alter database [OperationsLocal] set ansi_null_default off
go
alter database [OperationsLocal] set ansi_nulls off
go
alter database [OperationsLocal] set ansi_padding off
go
alter database [OperationsLocal] set ansi_warnings off
go
alter database [OperationsLocal] set arithabort off
go
alter database [OperationsLocal] set auto_close off
go
alter database [OperationsLocal] set auto_shrink off
go
alter database [OperationsLocal] set auto_update_statistics on
go
alter database [OperationsLocal] set cursor_close_on_commit off
go
alter database [OperationsLocal] set cursor_default global
go
alter database [OperationsLocal] set concat_null_yields_null off
go
alter database [OperationsLocal] set numeric_roundabort off
go
alter database [OperationsLocal] set quoted_identifier off
go
alter database [OperationsLocal] set recursive_triggers off 
go
alter database [OperationsLocal] set disable_broker 
go
alter database [OperationsLocal] set auto_update_statistics_async off 
go
alter database [OperationsLocal] set date_correlation_optimization off 
go
alter database [OperationsLocal] set trustworthy off 
go
alter database [OperationsLocal] set allow_snapshot_isolation off 
go
alter database [OperationsLocal] set parameterization simple
go
alter database [OperationsLocal] set read_committed_snapshot off 
go
alter database [OperationsLocal] set honor_broker_priority off 
go
alter database [OperationsLocal] set recovery full 
go
alter database [OperationsLocal] set multi_user 
go
alter database [OperationsLocal] set page_verify checksum  
go
alter database [OperationsLocal] set db_chaining off 
go
alter database [OperationsLocal] set filestream( non_transacted_access = off ) 
go
alter database [OperationsLocal] set target_recovery_time = 60 seconds
go
alter database [OperationsLocal] set delayed_durability = disabled 
go
alter database [OperationsLocal] set accelerated_database_recovery = off  
go
use [master]
go
alter database [OperationsLocal] set query_store = off
go
alter database [OperationsLocal] set read_write 
go
use OperationsLocal
go
create schema [rpt]
go
-- clear existing tables
if exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_NAME = 'FlowStates' and TABLE_SCHEMA = 'rpt')
   drop table [rpt].FlowStates;
go
if exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_NAME = 'OperationFlowStates' and TABLE_SCHEMA = 'rpt')
   drop table [rpt].OperationFlowStates;
go
if exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_NAME = 'OperationFlowStateHistory' and TABLE_SCHEMA = 'rpt')
   drop table [rpt].OperationFlowStateHistory;
go
-- create FlowStates table to define friendly names of allowed states
create table [rpt].[FlowStates](
	[FlowStateId] [tinyint] identity(0,1) not null,
	[FlowStateName] [nvarchar](100) not null,
 constraint [PK_rpt.FlowStates] primary key CLUSTERED 
(
	[FlowStateId] asc
) with (pad_index = off, statistics_norecompute = off, ignore_dup_key = off, allow_row_locks = on, allow_page_locks = on, optimize_for_sequential_key = off) ON [PRIMARY]
) on [primary]
go
-- populate FlowStates table
insert into rpt.FlowStates(FlowStateName) values ('unknown');
insert into rpt.FlowStates(FlowStateName) values ('Ready');
insert into rpt.FlowStates(FlowStateName) values ('Active');
insert into rpt.FlowStates(FlowStateName) values ('Redundant');
insert into rpt.FlowStates(FlowStateName) values ('Deferred');
insert into rpt.FlowStates(FlowStateName) values ('Completed');
insert into rpt.FlowStates(FlowStateName) values ('Stuck');
insert into rpt.FlowStates(FlowStateName) values ('Stalled');
insert into rpt.FlowStates(FlowStateName) values ('Failed');
insert into rpt.FlowStates(FlowStateName) values ('Finished');
-- create OperationFlowStates table to house runtime flow states
create table [rpt].[OperationFlowStates](
	[OperationFlowStateID] [bigint] identity(1,1) NOT NULL,
	[UniqueKey] [nvarchar](100) NOT NULL,
	[ActivityName] [nvarchar](100) NOT NULL,
	[TimeStarted] [datetime2](7) NULL,
	[TimeEnded] [datetime2](7) NULL,
	[TimeUpdated] [datetime2](7) NULL,
	[ActivityState] [tinyint] NULL,
	[ActivityStateName] [nvarchar](100) NULL,
	[Count] [int] NULL,
	[Notes] [nvarchar](max) NULL,
	[ProcessId] [nvarchar](100) NULL,
	[SequenceNumber] [int] NULL,
 constraint [PK_rpt.ReportFlowStates] primary key clustered 
(
	[OperationFlowStateID] asc
)with (pad_index = off, statistics_norecompute = off, ignore_dup_key = off, allow_row_locks = on, allow_page_locks = on, optimize_for_sequential_key = off) on [primary]
) on [primary] textimage_on [primary]
go
alter table [rpt].[OperationFlowStates] add constraint [DF_ReportFlowStates_ActivityState]  default ((0)) for [ActivityState]
go
-- create history table
create table [rpt].[OperationFlowStateHistory](
	[ReportFlowStateHistoryID] [bigint] identity(1,1) NOT NULL,
	[UniqueKey] [nvarchar](100) NOT NULL,
	[ActivityName] [nvarchar](100) NOT NULL,
	[TimeStarted] [datetime2](7) NULL,
	[TimeEnded] [datetime2](7) NULL,
	[TimeUpdated] [datetime2](7) NULL,
	[ActivityState] [tinyint] NULL,
	[ActivityStateName] [nvarchar](100) NULL,
	[Count] [int] NULL,
	[Notes] [nvarchar](max) NULL,
	[ProcessId] [nvarchar](100) NULL,
	[SequenceNumber] [int] NULL,
 constraint [PK_rpt.ReportFlowStateHistory] primary key clustered
(
	[ReportFlowStateHistoryID] asc
)with (pad_index = off, statistics_norecompute = off, ignore_dup_key = off, allow_row_locks = on, allow_page_locks = on, optimize_for_sequential_key = off) on [primary]
) on [primary] textimage_on [primary]
go
-- Stored procedures
create or alter procedure [rpt].[OperationFlowState_Read]
    @UniqueKey nvarchar(100)
as
begin
    set nocount on;
    select 
    (
	    select 
           UniqueKey,
           ActivityName,
           ActivityState,
           TimeStarted,
           TimeEnded,
           TimeUpdated,
           Notes,
           ProcessId,
           [Count],
           SequenceNumber
        from 
            rpt.OperationFlowStates
        where 
            UniqueKey = @UniqueKey
        for json path, without_array_wrapper) 
		as JsonResult
end
go
create or alter procedure [rpt].[OperationFlowState_Write]
    @json nvarchar(max)
as
begin
    set nocount on;

	declare @Timestamp datetime2 = GetUtcDate();
	
    merge rpt.OperationFlowStates as target
    using (select 
                json_value(@json, '$.UniqueKey') as UniqueKey,
                json_value(@json, '$.ActivityName') as ActivityName,
                json_value(@json, '$.TimeStarted') as TimeStarted,
                json_value(@json, '$.TimeEnded') as TimeEnded,
                json_value(@json, '$.ActivityState') as ActivityState,
				json_value(@json, '$.ActivityStateName') as ActivityStateName,
                json_value(@json, '$.Notes') as Notes,
                json_value(@json, '$.ProcessId') as ProcessId,
   				json_value(@json, '$.Count') as [Count],
                json_value(@json, '$.SequenceNumber') as SequenceNumber,
                @Timestamp as TimeUpdated
           ) as source
    on (target.UniqueKey = source.UniqueKey)
    when matched then
        update set 
            ActivityName = source.ActivityName,
            --TimeStarted = source.TimeStarted,
            TimeEnded = source.TimeEnded,
            ActivityState = source.ActivityState,
			ActivityStateName = source.ActivityStateName,
            Notes = source.Notes,
            ProcessId = source.ProcessId,
            SequenceNumber = source.SequenceNumber,
			[Count] = source.[Count],
            TimeUpdated = source.TimeUpdated
    -- we only save the start time in when the record is first made.
    when not matched then
        insert (UniqueKey, ActivityName, TimeStarted, TimeEnded, ActivityState, ActivityStateName, Notes, ProcessId, SequenceNumber, TimeUpdated, [Count])
        values (source.UniqueKey, source.ActivityName, @Timestamp, source.TimeEnded, source.ActivityState, source.ActivityStateName, source.Notes, source.ProcessId, source.SequenceNumber, source.TimeUpdated, source.[Count]);
;
 insert into rpt.OperationFlowStateHistory
 (
     UniqueKey, 
	 ActivityName, 
	 TimeStarted, 
	 TimeEnded, 
	 ActivityState, 
	 ActivityStateName, 
	 Notes, 
	 ProcessId, 
	 [Count],
	 SequenceNumber,
	 TimeUpdated
 )
 values 
 (
      json_value(@json, '$.UniqueKey'),
      json_value(@json, '$.ActivityName'), 
      json_value(@json, '$.TimeStarted'), 
      json_value(@json, '$.TimeEnded'), 
      json_value(@json, '$.ActivityState'), 
      json_value(@json, '$.ActivityStateName'), 
      json_value(@json, '$.Notes'), 
      json_value(@json, '$.ProcessId'), 
   	  json_value(@json, '$.Count'), 
      json_value(@json, '$.SequenceNumber'), 
      cast(@Timestamp as DateTime2)
)
end
go
create or alter procedure [rpt].[OperationFlowState_Purge]
as
begin
    set nocount on;
	delete from rpt.OperationFlowStates 
	where ActivityState = 9;
	-- rebuild any indexes
	alter index all on rpt.operationFlowStates rebuild;
end	
go
Create or alter procedure [rpt].[OperationFlowStateHistory_Purge]
as
begin
    set nocount on;
	truncate table rpt.OperationFlowStateHistory 
end	
go
