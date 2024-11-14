-----------------------------------------------------------------------------------------------------------------
-- stored procedures used by the metadata and by testing
-----------------------------------------------------------------------------------------------------------------
use [OperationsLocal]
go
set ansi_nulls on
go
set quoted_identifier on
go

create or alter   procedure [rpt].[ActivityCanRunNow_Check]
  @UniqueKey nvarchar(100)
as
begin
create table #Data
( 
  LoadFactor float, 
  MaximumDelayCount	int,
  RetryCount int,
  CurrentLoad float
);
insert #Data
exec rpt.LoadStateByUniqueKey_Read @UniqueKey;

select case
  when d.RetryCount >= d.MaximumDelayCount then 0
  when d.LoadFactor + d.CurrentLoad > 1.0 then 0
  else 1 end  MayRun
  from #data d;

drop table #data;
end
go

------------------------------------------------------
use [OperationsLocal];
go

/****** Object:  StoredProcedure [rpt].[ActivitySettings_Read]    Script Date: 11/8/2024 2:19:26 PM ******/
set ansi_nulls on;
go

set quoted_identifier on;
go

-- add read proc for settings
create or alter procedure [rpt].[ActivitySettings_Read] @ActivityName nvarchar(100)
as
begin
  with def as (
    select
        top (1) ActivitySettingsId,
        ActivityName,
        NumberOfRetries,
        InitialDelay,
        BackOffCoefficient,
        MaximumDelay,
        RetryTimeout,
        ActivityTimeout,
        LoadFactor,
        MaximumDelayCount,
        PartitionId
    from
        rpt.ActivitySettings
    where
        ActivityName = 'default'
  ),
  main as (
    select
        top (1) ActivitySettingsId,
        ActivityName,
        NumberOfRetries,
        InitialDelay,
        BackOffCoefficient,
        MaximumDelay,
        RetryTimeout,
        ActivityTimeout,
        LoadFactor,
        MaximumDelayCount,
        PartitionId
    from
        rpt.ActivitySettings
    where
        ActivityName = @ActivityName
  )
  select
    isnull(main.ActivityName, @ActivityName) ActivityName,
	isnull(main.NumberOfRetries, def.NumberOfRetries) NumberOfRetries,
    isnull(main.InitialDelay, def.InitialDelay) InitialDelay,
    isnull(main.BackOffCoefficient, def.BackOffCoefficient) BackOffCoefficient,
    isnull(main.MaximumDelay, def.MaximumDelay) MaximumDelay,
    isnull(main.RetryTimeout, def.RetryTimeout) RetryTimeout,
    isnull(main.ActivityTimeout, def.ActivityTimeout) ActivityTimeout,
    isnull(main.LoadFactor, def.LoadFactor) LoadFactor,
    isnull(main.MaximumDelayCount, def.MaximumDelayCount) MaximumDelayCount,
    isnull(main.PartitionId, def.PartitionId) PartitionId
  from
    def left join main on 1 = 1
    for json auto,   
      without_array_wrapper;
end
go

------------------------------------------------------

use [OperationsLocal]
go

/****** Object:  StoredProcedure [rpt].[ActivitySettings_Write]    Script Date: 11/8/2024 2:19:52 PM ******/
set ansi_nulls on;
go

set quoted_identifier on;
go

create or alter procedure [rpt].[ActivitySettings_Write] @json nvarchar(max) as 
begin
  set nocount on;
  merge rpt.ActivitySettings as target 
  using (
    select
        json_value(@json, '$.ActivityName') as ActivityName,
        json_value(@json, '$.NumberOfRetries') as NumberOfRetries,
        json_value(@json, '$.InitialDelay') as InitialDelay,
        json_value(@json, '$.BackOffCoefficient') as BackOffCoefficient,
        json_value(@json, '$.MaximumDelay') as MaximumDelay,
        json_value(@json, '$.RetryTimeout') as RetryTimeout,
        json_value(@json, '$.ActivityTimeout') as ActivityTimeout,
        json_value(@json, '$.LoadFactor') as LoadFactor,
        json_value(@json, '$.MaximumDelayCount') as MaximumDelayCount,
        json_value(@json, '$.PartitionId') as PartitionId
    ) as source on (target.ActivityName = source.ActivityName)
    when matched then
      update
      set
        ActivityName = source.ActivityName,
        NumberOfRetries = source.NumberOfRetries,
        InitialDelay = source.InitialDelay,
        BackOffCoefficient = source.BackOffCoefficient,
        MaximumDelay = source.MaximumDelay,
        RetryTimeout = source.RetryTimeout,
        ActivityTimeout = source.ActivityTimeout,
        LoadFactor = source.LoadFactor,
        MaximumDelayCount = source.MaximumDelayCount,
        PartitionId = source.PartitionId
    when not matched then
      insert
      (
        ActivityName,
        NumberOfRetries,
        InitialDelay,
        BackOffCoefficient,
        MaximumDelay,
        RetryTimeout,
        ActivityTimeout,
        LoadFactor,
        MaximumDelayCount,
        PartitionId
      )
      values
      (
        source.ActivityName,
        source.NumberOfRetries,
        source.InitialDelay,
        source.BackOffCoefficient,
        source.MaximumDelay,
        source.RetryTimeout,
        source.ActivityTimeout,
        source.LoadFactor,
        source.MaximumDelayCount,
        source.PartitionId
      );
end
go

------------------------------------------------------
use [OperationsLocal]
go

/****** Object:  StoredProcedure [rpt].[CurrentLoad_Read]    Script Date: 11/8/2024 2:20:13 PM ******/
set ansi_nulls on;
go

set quoted_identifier on;
go

create or alter procedure [rpt].[CurrentLoad_Read]
  @HostServer nvarchar(100) = 'SqlStateEmulator'
as
begin
select 
  isnull(sum(LoadFactor), 0) CurrentLoad,
  count(UniqueKey) ActiveCount
from 
  rpt.ActivitySettings as s 
right join rpt.OperationFlowStates fs 
on s.ActivityName = fs.ActivityName 
where HostServer = @HostServer
and (
ActivityStateName = 'Active' 
or ActivityStateName = 'Ready'
);
end
go
------------------------------------------------------

use [OperationsLocal];
go

/****** Object:  StoredProcedure [rpt].[EmulateActivity]    Script Date: 11/8/2024 2:20:50 PM ******/
set ansi_nulls on;
go

set quoted_identifier on;
go

create or alter procedure [rpt].[EmulateActivity]
  @UniqueKey nvarchar(100),       -- o--++
  @State nvarchar(3) = 'act',     -- Act, Red, Def, Com, Stu, Sta, Fai, Suc, Uns 
  @Activity nvarchar(1) = 'a',    -- a, b, c 
  @RetryCount int = 0             -- 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10

as
begin

set nocount on;

set ansi_nulls on;

set quoted_identifier on;

declare @StateId int;
select @StateId =
case when @State = 'rea' then 1
  when @State = 'Act' then 2
  when @State = 'Red' then 3
  when @State = 'Def' then 4
  when @State = 'Com' then 5
  when @State = 'Stu' then 6
  when @State = 'Sta' then 7
  when @State = 'Fai' then 8
  when @State = 'Suc' then 9
  when @State = 'Uns' then 10
end;

declare @StateName nvarchar(20);
select @StateName =
case 
  when @state = 'rea' then 'Ready'
  when @state = 'Act' then 'Active'
  when @state = 'Red' then 'Redundant'
  when @state = 'Def' then 'Deferred'
  when @state = 'Com' then 'Completed'
  when @state = 'Stu' then 'Stuck'
  when @state = 'Sta' then 'Stalled'
  when @state = 'Fai' then 'Failed'
  when @state = 'Suc' then 'Successful'
  when @state = 'Uns' then 'Unsuccessful'
end;

declare @ActivityName nvarchar(20);
select @ActivityName =
case 
  when @Activity = 'a' then 'ActivityAlpha'
  when @Activity = 'b' then 'ActivityBravo'
  when @Activity = 'c' then 'ActivityCharlie'
end;

merge rpt.OperationFlowStates as target using
(
    select 
      @UniqueKey as UniqueKey, 
      @StateId  as ActivityState, 
      @StateName as ActivityStateName,
      @ActivityName as ActivityName,
      'Emulated' + @UniqueKey as OperationName,
      @RetryCount as RetryCount,
      'SqlStateEmulator' as HostServer,
      100 as SequenceNumber,
        dateadd(minute, -10, getutcdate()) as TimeStarted,
        getutcdate() as TimeEnded,
        getutcdate() as TimeUpdated,
		'Emulated' as Reason,
		'Emulated' as trace,
		@UniqueKey as InstanceId
) as source on (target.UniqueKey = source.UniqueKey)
when matched then
  update set
    ActivityState = source.ActivityState,
    ActivityStateName = source.ActivityStateName,
    ActivityName = source.ActivityName,
    OperationName = source.OperationName,
    RetryCount = source.RetryCount,
    HostServer = source.HostServer,
    SequenceNumber = source.SequenceNumber,
    TimeStarted = source.TimeStarted,
    TimeEnded = source.TimeEnded,
    TimeUpdated = source.TimeUpdated,
	Reason = source.Reason,
	Trace = source.Trace,
	InstanceId = source.UniqueKey
when not matched then
  insert 
  (
    UniqueKey, 
    ActivityState, 
    ActivityStateName, 
    ActivityName, 
    OperationName, 
    RetryCount, 
    HostServer, 
    SequenceNumber, 
    TimeStarted, 
    TimeEnded, 
    TimeUpdated,
	Reason,
	Trace,
	InstanceId
)
  values 
(
    source.UniqueKey, 
    source.ActivityState, 
    source.ActivityStateName, 
    source.ActivityName, 
    source.OperationName, 
    source.RetryCount, 
    source.HostServer, 
    source.SequenceNumber, 
    source.TimeStarted, 
    source.TimeEnded, 
    source.TimeUpdated,
	source.Reason,
	source.Trace,
	source.InstanceId
);
end
go

------------------------------------------------------

use [OperationsLocal]
go

/****** Object:  StoredProcedure [rpt].[LoadSettings_Read]    Script Date: 11/8/2024 2:21:08 PM ******/
set ansi_nulls on;
go

set quoted_identifier on;
go

create or alter procedure [rpt].[LoadSettings_Read]
  @ActivityName nvarchar(100)
as
begin 
select top 1
  isnull(LoadFactor, DefaultLoadFactor) LoadFactor, 
  isNull(MaximumDelayCount, DefaultMaximumDelayCount) MaximumDelayCount
from rpt.ActivitySettings s 
left join 
(
  select 
  LoadFactor DefaultLoadfactor, 
  MaximumDelayCount DefaultMaximumDelayCount 
from rpt.ActivitySettings d 
where d.ActivityName = 'Default'
) as D  on 1=1
where ActivityName = @ActivityName;
end
go

------------------------------------------------------

use [OperationsLocal]
go

/****** Object:  StoredProcedure [rpt].[LoadSettings_ReadAll]    Script Date: 11/8/2024 2:21:34 PM ******/
set ansi_nulls on;
go

set quoted_identifier on;
go

create or alter procedure [rpt].[LoadSettings_ReadAll]
as
begin
select 
  ActivityName, 
  isnull(LoadFactor, DefaultLoadFactor) LoadFactor, 
  isNull(MaximumDelayCount, DefaultMaximumDelayCount) MaximumDelayCount
from rpt.ActivitySettings s 
left join 
(
  select 
  LoadFactor DefaultLoadfactor, 
  MaximumDelayCount DefaultMaximumDelayCount 
from rpt.ActivitySettings d 
where d.ActivityName = 'Default'
) as D on 1=1;
end
go

------------------------------------------------------

use [OperationsLocal];
go

/****** Object:  StoredProcedure [rpt].[LoadStateByUniqueKey_Read]    Script Date: 11/8/2024 2:22:13 PM ******/
set ansi_nulls on;
go

set quoted_identifier on;
go

create or alter procedure [rpt].[LoadStateByUniqueKey_Read]
   @UniqueKey nvarchar(100)
as begin
declare @HostServer nvarchar(100);
select @HostServer = HostServer from rpt.OperationFlowStates where UniqueKey = @UniqueKey;

with aa as (
select
  isnull(LoadFactor, DefaultLoadFactor) LoadFactor, 
  isNull(MaximumDelayCount, DefaultMaximumDelayCount) MaximumDelayCount,
  RetryCount
  from rpt.ActivitySettings s join rpt.OperationFlowStates f
  on s.ActivityName = f.ActivityName
  left join 
(
  select 
  LoadFactor DefaultLoadfactor, 
  MaximumDelayCount DefaultMaximumDelayCount 
  from rpt.ActivitySettings d 
  where d.ActivityName = 'Default'
) as xxx on 1=1
where f.UniqueKey = @UniqueKey
) 
, bb as
(
select 
  sum(loadfactor) CurrentLoad
  from rpt.ActivitySettings s left join rpt.OperationFlowStates f
  on s.ActivityName = f.ActivityName
  where ActivityStateName in ('Active', 'Ready')
  and HostServer = @HostServer
) 
select * from aa join bb on 1=1;
end
go

------------------------------------------------------

use [OperationsLocal]
go

/****** Object:  StoredProcedure [rpt].[OperationFlowState_Purge]    Script Date: 11/8/2024 2:22:35 PM ******/
set ansi_nulls on
go

set quoted_identifier on
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

------------------------------------------------------

use [OperationsLocal];
go

/****** Object:  StoredProcedure [rpt].[OperationFlowState_Read]    Script Date: 11/8/2024 2:22:56 PM ******/
set ansi_nulls on;
go

set quoted_identifier on
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
            InstanceId,
            [RetryCount],
            SequenceNumber,
            Disruptions,
            HostServer,
            PrevailingLoadFactor
        from rpt.OperationFlowStates
        where UniqueKey = @UniqueKey for json path,
            without_array_wrapper
    ) as JsonResult;
end;
go

------------------------------------------------------

use [OperationsLocal];
go

/****** Object:  StoredProcedure [rpt].[OperationFlowState_Write]    Script Date: 11/8/2024 2:23:27 PM ******/
set ansi_nulls on;
go

set quoted_identifier on;
go

create or alter procedure [rpt].[OperationFlowState_Write] @json nvarchar(max) as 
begin
set nocount on;
declare @Timestamp datetime2 = GetUtcDate();
declare @PrevailingLoadFactor float = (
    select sum(LoadFactor)
    from rpt.ActivitySettings as s
    right join rpt.OperationFlowStates fs
    on s.ActivityName = fs.ActivityName
    where ActivityStateName in ('Active', 'Ready')
    and HostServer = json_value(@json, '$.HostServer')
);
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
        json_value(@json, '$.InstanceId') as InstanceId,
        json_value(@json, '$.RetryCount') as [RetryCount],
        json_value(@json, '$.SequenceNumber') as SequenceNumber,
        json_value(@json, '$.Disruptions') as Disruptions,
        json_value(@json, '$.HostServer') as HostServer,
        @PrevailingLoadFactor as PrevailingLoadFactor,
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
    InstanceId = source.InstanceId,
    SequenceNumber = source.SequenceNumber,
    [RetryCount] = source.[RetryCount],
    Disruptions = source.Disruptions,
    HostServer = source.HostServer,
    PrevailingLoadFactor = source.PrevailingLoadFactor,
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
        InstanceId,
        SequenceNumber,
        Disruptions,
        HostServer,
        PrevailingLoadFactor,
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
        source.InstanceId,
        source.SequenceNumber,
        source.Disruptions,
        source.HostServer,
        PrevailingLoadFactor,
        source.TimeUpdated,
        source.[RetryCount]
    );

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
        InstanceId,
        [RetryCount],
        SequenceNumber,
        Disruptions,
        HostServer,
        PrevailingLoadFactor,
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
        json_value(@json, '$.InstanceId'),
        json_value(@json, '$.RetryCount'),
        json_value(@json, '$.SequenceNumber'),
        json_value(@json, '$.Disruptions'),
        json_value(@json, '$.HostServer'),
        @PrevailingLoadFactor,
        cast(@Timestamp as DateTime2)
    );
-- if finished, the retry count is the max of all retries    
if (json_value(@json, '$.ActivityState') > 8)
begin
   declare @maxRetries int = (
      select max(RetryCount) 
      from rpt.OperationFlowStateHistory 
      where UniqueKey = json_value(@json, '$.UniqueKey')
   );
update rpt.OperationFlowStates 
set RetryCount = @maxRetries 
where UniqueKey = json_value(@json, '$.UniqueKey');
-- then we also want to copy this record to the final table
-- but the record in the main table will remain until we purge, 
-- to prevent reentrancy
insert into rpt.OperationFlowStatesFinal (
        UniqueKey,
        OperationName,
        ActivityName,
        TimeStarted,
        TimeEnded,
        ActivityState,
        ActivityStateName,
        Trace,
        Reason,
        InstanceId,
        [RetryCount],
        SequenceNumber,
        Disruptions,
        HostServer,
        PrevailingLoadFactor,
        TimeUpdated
    )
select 
        UniqueKey,
        OperationName,
        ActivityName,
        TimeStarted,
        TimeEnded,
        ActivityState,
        ActivityStateName,
        Trace,
        Reason,
        InstanceId,
        [RetryCount],
        SequenceNumber,
        Disruptions,
        HostServer,
        PrevailingLoadFactor,
        TimeUpdated
     from rpt.OperationFlowStates 
where UniqueKey = json_value(@json, '$.UniqueKey');
end
end;
go

------------------------------------------------------

use [OperationsLocal];
go

/****** Object:  StoredProcedure [rpt].[OperationFlowStateHistory_Purge]    Script Date: 11/8/2024 2:23:44 PM ******/
set ansi_nulls on;
go

set quoted_identifier on;
go

create or alter procedure [rpt].[OperationFlowStateHistory_Purge] as 
begin
set nocount on;
truncate table rpt.OperationFlowStateHistory;
end;
GO

------------------------------------------------------