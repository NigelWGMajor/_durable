-- FlowSettings_AllLoadFactors_Select

create or alter procedure [rpt].[FlowSettings_AllLoadFactors_Select]
as
begin
select 
  ActivityName, 
  isnull(LoadFactor, DefaultLoadFactor) LoadFactor, 
  isNull(MaximumDelayCount, DefaultMaximumDelayCount) MaximumDelayCount
from rpt.FlowActivitySettings s 
left join 
(
  select 
  LoadFactor DefaultLoadfactor, 
  MaximumDelayCount DefaultMaximumDelayCount 
from rpt.FlowActivitySettings d 
where d.ActivityName = 'Default'
) as D on 1=1;
end
go
