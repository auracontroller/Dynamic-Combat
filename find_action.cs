// To make an agent block, Bannerlord generally uses:
// attacker.SetDefendState(Agent.UsageDirection.DefendDown);
// Or ActionIndexCache and SetActionChannel. We can use SetActionChannel(1, ActionIndexCache.act_none...) to stop attack,
// and attacker.EnforceShieldUsage(Agent.UsageDirection.DefendDown) or similar.
