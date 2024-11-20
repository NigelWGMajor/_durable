-- FlowStatesFinal table holds only final (Successful or Unsuccessful) records for each operation

if exists(
    select *
    from INFORMATION_SCHEMA.TABLES
    where TABLE_NAME = 'FlowStatesFinal'
      and TABLE_SCHEMA = 'rpt'
  ) 
  drop table [rpt].FlowStatesFinal;
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

-- ----------------------------------------------