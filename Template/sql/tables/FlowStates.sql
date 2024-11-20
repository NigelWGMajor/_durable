-- FlowStates table associates ActivityStates (int) with their names

if exists(
    select *
    from INFORMATION_SCHEMA.TABLES
    where TABLE_NAME = 'FlowStates'
      and TABLE_SCHEMA = 'rpt'
  )
  drop table [rpt].FlowStates;
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

insert into rpt.FlowStates(FlowStateName)
values ('unknown');
go
insert into rpt.FlowStates(FlowStateName)
values ('Ready');
go
insert into rpt.FlowStates(FlowStateName)
values ('Active');
go
insert into rpt.FlowStates(FlowStateName)
values ('Redundant');
go
insert into rpt.FlowStates(FlowStateName)
values ('Deferred');
go
insert into rpt.FlowStates(FlowStateName)
values ('Completed');
go
insert into rpt.FlowStates(FlowStateName)
values ('Stuck');
go
insert into rpt.FlowStates(FlowStateName)
values ('Stalled');
go
insert into rpt.FlowStates(FlowStateName)
values ('Failed');
go
insert into rpt.FlowStates(FlowStateName)
values ('Successful');
go
insert into rpt.FlowStates(FlowStateName)
values ('Unsuccessful');
go

-- -----------------------------------------------------