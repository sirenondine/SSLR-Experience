Scriptname SSLR_XPRateEffect extends ActiveMagicEffect
{SSLR-Experience: registers this effect's XP multiplier with the controller while active.}

float Property Mult = 1.0 Auto
SSLR_XPRateController Property Controller Auto

Event OnEffectStart(Actor akTarget, Actor akCaster)
	if Controller && akTarget == Game.GetPlayer()
		Controller.AddRate(Mult)
	endif
EndEvent

Event OnEffectFinish(Actor akTarget, Actor akCaster)
	if Controller && akTarget == Game.GetPlayer()
		Controller.RemoveRate(Mult)
	endif
EndEvent
