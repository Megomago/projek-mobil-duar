using UnityEngine;

public readonly struct OptResult
{
    public readonly float damage;
    public readonly bool pierce;
    public readonly float exitVel;
    public readonly float remainingPen;
    public readonly float remainingAtk; // Tambahan wajib buat nampung sisa ATK

    public OptResult(float dmg, bool p, float v, float remPen, float remAtk)
    {
        damage = dmg;
        pierce = p;
        exitVel = v;
        remainingPen = remPen;
        remainingAtk = remAtk;
    }
}

public static class OptFormula
{
    public static OptResult Calculate(float atk, float pen, float def, float muzzleVel)
    {
        // Kalo peluru udah ampas dari sananya, stop aja.
        if (atk <= 0f || pen <= 0f) 
        {
            return new OptResult(0f, false, 0f, 0f, 0f);
        }

        // Kalo target gak pake baju (DEF 0), bantai abis. Tembus murni tanpa ngurangin stats peluru.
        if (def <= 0f)
        {
            return new OptResult(atk * 2f, true, muzzleVel, pen, atk); 
        }

        float ratio = pen / def;

        // 1. PEN cupu, gak nembus armor sama sekali. Gak ada damage.
        if (ratio < 1.0f)
        {
            return new OptResult(0f, false, 0f, 0f, 0f);
        }

        // 2. Kalkulasi effective ATK (Bonus max 100%)
        float bonusPercent = Mathf.Clamp(ratio - 1.0f, 0f, 1.0f);
        float effectiveAtk = atk * (1f + bonusPercent);

        // 3. Kalo nembus tapi gak nyampe 200% DEF, damage full masuk, tapi peluru berenti disini.
        if (ratio < 2.0f)
        {
            return new OptResult(effectiveAtk, false, 0f, 0f, 0f);
        }

        // 4. TEMBUS (Pierce == true) untuk game ARCADE
        
        // Target PERTAMA dapet DAMAGE FULL (Gak dikali velRatio lagi! Biar player puas liat demeg gede)
        float finalDmg = effectiveAtk; 
        
        // Kalkulasi seberapa ngeden pelurunya nembus target ini
        float resistance = (def / pen) * 2f; 
        float velRatio = Mathf.Max(0f, 1f - resistance); // Berapa persen tenaga peluru yang tersisa

        // SISA STATS buat dilempar ke objek di belakangnya
        float exitVel = muzzleVel * velRatio;
        
        // ATK dan PEN disunat sesuai tenaga yang abis buat nembus objek pertama
        float remainingAtk = atk * velRatio; 
        float remainingPen = Mathf.Max(0f, pen - def);

        return new OptResult(finalDmg, true, exitVel, remainingPen, remainingAtk);
    }
}