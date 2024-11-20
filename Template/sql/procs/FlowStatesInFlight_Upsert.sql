-- FlowStatesInFlight_Upsert

create or alter procedure [rpt].[FlowStatesInFlight_Upsert] @json nvarchar(max) as 
begin
set nocount on;
declare @Timestamp datetime2 = GetUtcDate();
declare @PrevailingLoadFactor float = (
    select isnull(sum(LoadFactor), 0)
    from rpt.FlowActivitySettings as s
    right join rpt.FlowStatesInFlight fs
    on s.ActivityName = fs.ActivityName
    where ActivityStateName in ('Active', 'Ready')
    and HostServer = json_value(@json, '$.HostServer')
);
merge rpt.FlowStatesInFlight as target using (
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

insert into rpt.FlowStatesHistory (
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
      from rpt.FlowStatesHistory 
      where UniqueKey = json_value(@json, '$.UniqueKey')
   );
update rpt.FlowStatesInFlight 
set RetryCount = @maxRetries 
where UniqueKey = json_value(@json, '$.UniqueKey');
-- then we also want to copy this record to the final table
-- but the record in the main table will remain until we purge, 
-- to prevent reentrancy
insert into rpt.FlowStatesFinal (
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
     from rpt.FlowStatesInFlight 
where UniqueKey = json_value(@json, '$.UniqueKey');
end
end;
go
