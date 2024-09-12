set ansi_nulls on
go
set quoted_identifier on
go 

if exists (select * from sys.objects where object_id = object_id(N'rpt.ReportFlowState_Read') and type in (N'P', N'PC'))
begin
    drop procedure rpt.ReportFlowState_Read;
    print '** Existing procedure dropped **';
end
go

create procedure rpt.ReportFlowState_Read
    @KeyId nvarchar(100)
as
begin
    set nocount on;
    select 
    (
	    select 
           KeyId,
           ActivityName,
           ActivityState,
           TimeStarted,
           TimeEnded,
           TimeUpdated,
           Notes,
           ProcessId,
           Instances
        from 
            rpt.ReportFlowStates
        where 
            KeyId = @KeyID
        for json path, without_array_wrapper) 
		as JsonResult
end
go

-- exec rpt.ReportFlowState_Read 1