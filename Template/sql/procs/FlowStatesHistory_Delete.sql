-- FlowStatesHistory_Delete

create or alter procedure [rpt].[FlowStatesHistory_Delete] as 
begin
set nocount on;
truncate table rpt.FlowStatesHistory;
end;
go
