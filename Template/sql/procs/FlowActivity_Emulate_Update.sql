-- FlowActivity_Emulate_Update

create or alter procedure [rpt].[FlowActivity_Emulate_Update]
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

merge rpt.FlowStatesInFlight as target using
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