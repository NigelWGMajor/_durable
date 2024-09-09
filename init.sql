-- modify database to suit
-- this drops and recreates a populated FlowStates table and an empty ReportFlowStates table in the rpt schema.

use [ReportsLocal] 
go
set ansi_nulls on
go
set quoted_identifier on
go
-- clear existing tables
if exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_NAME = 'FlowStates' and TABLE_SCHEMA = 'rpt')
   drop table [rpt].FlowStates;
go
if exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_NAME = 'ReportFlowStates' and TABLE_SCHEMA = 'rpt')
   drop table [rpt].ReportFlowStates;
go
-- create FlowStates table to define allowed states
create table [rpt].[FlowStates](
	[FlowStateId] [tinyint] identity(0,1) not null,
	[FlowStateName] [nvarchar](100) not null,
 constraint [PK_rpt.FlowStates] primary key CLUSTERED 
(
	[FlowStateId] asc
)with (pad_index = off, statistics_norecompute = off, ignore_dup_key = off, allow_row_locks = on, allow_page_locks = on, optimize_for_sequential_key = off) ON [PRIMARY]
) on [primary]
go
-- populate FlowStates table
insert into rpt.FlowStates(FlowStateName) values ('unknown');
insert into rpt.FlowStates(FlowStateName) values ('Stuck');
insert into rpt.FlowStates(FlowStateName) values ('Deferred');
insert into rpt.FlowStates(FlowStateName) values ('Active');
insert into rpt.FlowStates(FlowStateName) values ('Completed');
insert into rpt.FlowStates(FlowStateName) values ('Stalled');
insert into rpt.FlowStates(FlowStateName) values ('Failed');
-- create ReportFlowStates table to house runtime flow states
create table [rpt].[ReportFlowStates](
	[ReportFlowStateID] [bigint] not null,
	[Key] [bigint] not null,
	[ActivityName] [nvarchar](100) not null,
	[ActivityTime] [datetimeoffset](7) null,
	[ActivityState] [tinyint] null,
	[Sequence] [int] null,
	[Message] [nvarchar](500) null,
 constraint [PK_rpt.ReportFlowStates] primary key clustered 
(
	[ReportFlowStateID] asc
)with (pad_index = off, statistics_norecompute = off, ignore_dup_key = off, allow_row_locks = on, allow_page_locks = on, optimize_for_sequential_key = off) on [primary]
) on [primary]
go
alter table [rpt].[ReportFlowStates] add  constraint [DF_ReportFlowStates_ActivityState]  default ((0)) for [ActivityState]
go
