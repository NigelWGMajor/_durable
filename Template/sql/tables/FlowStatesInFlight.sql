-- FlowStatesInFlight table with one record per operatoin in flight

if exists(
    select *
    from INFORMATION_SCHEMA.TABLES
    where TABLE_NAME = 'FlowStatesInFlight'
      and TABLE_SCHEMA = 'rpt'
  ) 
  drop table [rpt].FlowStatesInFlight;
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

-- ------------------------------------------