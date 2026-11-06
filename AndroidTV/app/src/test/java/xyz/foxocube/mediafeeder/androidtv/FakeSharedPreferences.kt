package xyz.foxocube.mediafeeder.androidtv

import android.content.SharedPreferences

/**
 * In-memory [SharedPreferences] fake for pure JVM unit tests (no Robolectric, no androidx.test).
 */
@Suppress("UNCHECKED_CAST")
class FakeSharedPreferences : SharedPreferences {
    private val values: HashMap<String?, Any?> = HashMap()

    override fun getAll(): Map<String, *> = values.filterKeys { it != null }.mapKeys { it.key!! }

    override fun getString(key: String?, defValue: String?): String? = values[key] as? String ?: defValue

    override fun getStringSet(key: String?, defValues: MutableSet<String>?): MutableSet<String> =
        values[key] as? MutableSet<String> ?: defValues ?: mutableSetOf()

    override fun getInt(key: String?, defValue: Int): Int = values[key] as? Int ?: defValue

    override fun getLong(key: String?, defValue: Long): Long = values[key] as? Long ?: defValue

    override fun getFloat(key: String?, defValue: Float): Float = values[key] as? Float ?: defValue

    override fun getBoolean(key: String?, defValue: Boolean): Boolean = values[key] as? Boolean ?: defValue

    override fun contains(key: String?): Boolean = values.containsKey(key)

    override fun registerOnSharedPreferenceChangeListener(
        listener: SharedPreferences.OnSharedPreferenceChangeListener?,
    ) {
        // No-op for the fake.
    }

    override fun unregisterOnSharedPreferenceChangeListener(
        listener: SharedPreferences.OnSharedPreferenceChangeListener?,
    ) {
        // No-op for the fake.
    }

    override fun edit(): SharedPreferences.Editor = FakeEditor(values)
}

/** [SharedPreferences.Editor] that applies pending edits synchronously to a backing map. */
private class FakeEditor(private val values: HashMap<String?, Any?>) : SharedPreferences.Editor {
    private val pending: HashMap<String?, Any?> = HashMap()
    private val removed: HashSet<String?> = HashSet()

    override fun putString(key: String?, value: String?): SharedPreferences.Editor = add(key, value)

    override fun putStringSet(key: String?, values: MutableSet<String>?): SharedPreferences.Editor = add(key, values)

    override fun putInt(key: String?, value: Int): SharedPreferences.Editor = add(key, value)

    override fun putLong(key: String?, value: Long): SharedPreferences.Editor = add(key, value)

    override fun putFloat(key: String?, value: Float): SharedPreferences.Editor = add(key, value)

    override fun putBoolean(key: String?, value: Boolean): SharedPreferences.Editor = add(key, value)

    override fun remove(key: String?): SharedPreferences.Editor {
        removed.add(key)
        return this
    }

    override fun clear(): SharedPreferences.Editor {
        removed.addAll(values.keys)
        return this
    }

    override fun commit(): Boolean {
        flush()
        return true
    }

    override fun apply() {
        flush()
    }

    private fun add(key: String?, value: Any?): SharedPreferences.Editor {
        pending[key] = value
        removed.remove(key)
        return this
    }

    private fun flush() {
        removed.forEach(values::remove)
        values.putAll(pending)
        pending.clear()
        removed.clear()
    }
}
