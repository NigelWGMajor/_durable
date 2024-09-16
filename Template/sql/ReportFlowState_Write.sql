set ansi_nulls on
go
set quoted_identifier on
go 

if exists (select * from sys.objects where object_id = object_id(N'rpt.ReportFlowState_Write') and type in (N'P', N'PC'))
begin
    drop procedure rpt.ReportFlowState_Write;
    print '** Existing procedure dropped **';
end
go

create procedure rpt.ReportFlowState_Write
    @json nvarchar(max)
as
begin
    set nocount on;

    merge rpt.ReportFlowStates as target
    using (select 
                json_value(@json, '$.KeyId') as KeyId,
                json_value(@json, '$.ActivityName') as ActivityName,
                json_value(@json, '$.TimeStarted') as TimeStarted,
                json_value(@json, '$.TimeEnded') as TimeEnded,
                json_value(@json, '$.ActivityState') as ActivityState,
                json_value(@json, '$.Notes') as Notes,
                json_value(@json, '$.ProcessId') as ProcessId,
                json_value(@json, '$.InstanceNumber') as InstanceNumber,
                GetUtcDate() as TimeUpdated
           ) as source
    on (target.KeyId = source.KeyId)
    when matched then
        update set 
            ActivityName = source.ActivityName,
            TimeStarted = source.TimeStarted,
            TimeEnded = source.TimeEnded,
            ActivityState = source.ActivityState,
            Notes = source.Notes,
            ProcessId = source.ProcessId,
            InstanceNumber = source.InstanceNumber,
            TimeUpdated = source.TimeUpdated
    when not matched then
        insert (KeyId, ActivityName, TimeStarted, TimeEnded, ActivityState, Notes, ProcessId, InstanceNumber, TimeUpdated)
        values (source.KeyId, source.ActivityName, source.TimeStarted, source.TimeEnded, source.ActivityState, source.Notes, source.ProcessId, source.InstanceNumber, source.TimeUpdated);
end
go