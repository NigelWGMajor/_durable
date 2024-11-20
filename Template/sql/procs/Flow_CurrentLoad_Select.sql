-- Flow_CurrentLoad_Select

create or alter procedure [rpt].[Flow_CurrentLoad_Select]
  @HostServer nvarchar(100) = 'SqlStateEmulator'
as
begin
select 
  isnull(sum(LoadFactor), 0) CurrentLoad,
  count(UniqueKey) ActiveCount
from 
  rpt.FlowActivitySettings as s 
right join rpt.FlowStatesInFlight fs 
on s.ActivityName = fs.ActivityName 
where HostServer = @HostServer
and (
ActivityStateName = 'Active' 
or ActivityStateName = 'Ready'
);
end
go