import { useState } from "react";
import {
  ActivityIndicator,
  KeyboardAvoidingView,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from "react-native";
import { api } from "../src/api";
import { LanguagePicker } from "../src/LanguagePicker";
import { useLanguages } from "../src/LanguagesContext";

export default function TextScreen() {
  const languages = useLanguages();
  const [source, setSource] = useState("auto");
  const [target, setTarget] = useState("en");
  const [text, setText] = useState("");
  const [output, setOutput] = useState("");
  const [busy, setBusy] = useState(false);

  async function run() {
    if (!text.trim()) return;
    setBusy(true);
    setOutput("");
    try {
      const r = await api.translate({ text, source, target });
      setOutput(r.translated);
    } catch (e) {
      setOutput(`Error: ${(e as Error).message}`);
    } finally {
      setBusy(false);
    }
  }

  return (
    <KeyboardAvoidingView
      behavior={Platform.OS === "ios" ? "padding" : undefined}
      style={styles.container}
    >
      <ScrollView contentContainerStyle={styles.inner}>
        <View style={styles.row}>
          <LanguagePicker value={source} onChange={setSource} languages={languages} />
          <Text style={styles.arrow}>→</Text>
          <LanguagePicker value={target} onChange={setTarget} languages={languages} excludeAuto />
        </View>

        <TextInput
          value={text}
          onChangeText={setText}
          placeholder="Enter text…"
          multiline
          style={styles.input}
        />

        <Pressable onPress={run} disabled={busy} style={[styles.btn, busy && { opacity: 0.6 }]}>
          {busy ? <ActivityIndicator color="#fff" /> : <Text style={styles.btnText}>Translate</Text>}
        </Pressable>

        <View style={styles.output}>
          <Text style={styles.outputLabel}>Translation</Text>
          <Text style={styles.outputText}>{output || "—"}</Text>
        </View>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: "#f8fafc" },
  inner: { padding: 16, gap: 12 },
  row: { flexDirection: "row", alignItems: "center", gap: 8 },
  arrow: { fontSize: 18, color: "#64748b" },
  input: {
    minHeight: 140,
    borderWidth: 1,
    borderColor: "#cbd5e1",
    borderRadius: 10,
    backgroundColor: "#fff",
    padding: 12,
    fontSize: 15,
    textAlignVertical: "top",
  },
  btn: {
    backgroundColor: "#4f46e5",
    paddingVertical: 12,
    borderRadius: 10,
    alignItems: "center",
  },
  btnText: { color: "#fff", fontWeight: "600", fontSize: 15 },
  output: {
    minHeight: 140,
    borderWidth: 1,
    borderColor: "#e2e8f0",
    borderRadius: 10,
    backgroundColor: "#fff",
    padding: 12,
  },
  outputLabel: { fontSize: 12, color: "#64748b", fontWeight: "600", marginBottom: 6 },
  outputText: { fontSize: 15, color: "#0f172a" },
});
