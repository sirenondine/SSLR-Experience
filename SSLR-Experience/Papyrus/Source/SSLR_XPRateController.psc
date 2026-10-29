Scriptname SSLR_XPRateController extends Quest
{SSLR-Experience: scales the player's character XP gains by the product of all active
skill-rate multipliers (Rested, Lover's Comfort, ...), which do nothing under Static Skill Leveling.
Polls only while at least one multiplier is active.}

float Property PollInterval = 1.0 Auto

float[] mults
int count = 0
float lastXP = 0.0

Event OnInit()
	mults = new float[64]
	lastXP = Game.GetPlayerExperience()
EndEvent

Function AddRate(float m)
	Settle()
	if !mults
		mults = new float[64]
	endif
	if count >= mults.Length
		return
	endif
	mults[count] = m
	count += 1
	if count == 1
		RegisterForSingleUpdate(PollInterval)
	endif
EndFunction

Function RemoveRate(float m)
	Settle()
	int i = 0
	while i < count
		if mults[i] == m
			count -= 1
			mults[i] = mults[count]
			mults[count] = 0.0
			return
		endif
		i += 1
	endwhile
EndFunction

float Function Product()
	float p = 1.0
	int i = 0
	while i < count
		p *= mults[i]
		i += 1
	endwhile
	if p < 0.0
		p = 0.0
	endif
	return p
EndFunction

; apply the current multiplier to XP gained since the last check; a drop (level-up) just resyncs
Function Settle()
	float xp = Game.GetPlayerExperience()
	float delta = xp - lastXP
	if delta > 0.0 && count > 0
		float p = Product()
		if p != 1.0
			xp += delta * (p - 1.0)
			Game.SetPlayerExperience(xp)
		endif
	endif
	lastXP = xp
EndFunction

Event OnUpdate()
	Settle()
	if count > 0
		RegisterForSingleUpdate(PollInterval)
	endif
EndEvent
