-- FlowStatesInFlight_Select

create or alter procedure [rpt].[FlowStatesInFlight_Select] @UniqueKey nvarchar(100) as 
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
        from rpt.FlowStatesInFlight
        where UniqueKey = @UniqueKey for json path,
            without_array_wrapper
    ) as JsonResult;
end;
go
