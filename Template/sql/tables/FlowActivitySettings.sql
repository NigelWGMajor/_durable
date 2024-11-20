-- FlowActivitySettings stores settings for each configuration

if exists(
    select *
    from INFORMATION_SCHEMA.TABLES
    where TABLE_NAME = 'FlowActivitySettings'
      and TABLE_SCHEMA = 'rpt'
  ) 
  drop table [rpt].FlowActivitySettings;
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

-- --------------------------------------------------
