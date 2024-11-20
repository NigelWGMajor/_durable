-- FlowStatesInFlight_Final_Delete

create or alter procedure [rpt].[FlowStatesInFlight_Final_Delete] as 
begin
set nocount on;
delete from rpt.FlowStatesInFlight
where ActivityState = 9 or ActivityState = 10;
-- rebuild any indexes
alter index all on rpt.FlowStatesInFlight rebuild;
end;
go