-- FlowSettings_LoadFactor_Select

create or alter procedure [rpt].[FlowSettings_LoadFactor_Select]
  @ActivityName nvarchar(100)
as
begin 
select top 1
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
) as D  on 1=1
where ActivityName = @ActivityName;
end
go
