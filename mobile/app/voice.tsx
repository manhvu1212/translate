import { Audio } from "expo-av";
import * as FileSystem from "expo-file-system";
import { useRef, useState } from "react";
import {
  ActivityIndicator,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from "react-native";
import { api } from "../src/api";
import { LanguagePicker } from "../src/LanguagePicker";
import { useLanguages } from "../src/LanguagesContext";

export default function VoiceScreen() {
  const languages = useLanguages();
  const [source, setSource] = useState("auto");
  const [target, setTarget] = useState("en");
  const [recording, setRecording] = useState<Audio.Recording | null>(null);
  const [busy, setBusy] = useState(false);
  const [transcript, setTranscript] = useState("");
  const [translated, setTranslated] = useState("");
  const isRec = useRef(false);

  async function toggle() {
    if (isRec.current) {
      await stop();
    } else {
      await start();
    }
  }

  async function start() {
    const perm = await Audio.requestPermissionsAsync();
    if (!perm.granted) return;
    await Audio.setAudioModeAsync({ allowsRecordingIOS: true, playsInSilentModeIOS: true });
    const rec = new Audio.Recording();
    await rec.prepareToRecordAsync(Audio.RecordingOptionsPresets.HIGH_QUALITY);
    await rec.startAsync();
    setRecording(rec);
    isRec.current = true;
  }

  async function stop() {
    if (!recording) return;
    isRec.current = false;
    await recording.stopAndUnloadAsync();
    const uri = recording.getURI();
    setRecording(null);
    if (!uri) return;
    await submit(uri);
  }

  async function submit(uri: string) {
    setBusy(true);
    setTranscript("");
    setTranslated("");
    try {
      const b64 = await FileSystem.readAsStringAsync(uri, {
        encoding: FileSystem.EncodingType.Base64,
      });
      const mime = uri.endsWith(".m4a") ? "audio/m4a" : "audio/mp4";
      const binary = atob(b64);
      const bytes = new Uint8Array(binary.length);
      for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
      const blob = new Blob([bytes], { type: mime });
      const r = await api.stt(blob, { language: source, target, filename: "recording.m4a" });
      setTranscript(r.text);
      setTranslated(r.translated ?? "");
    } catch (e) {
      setTranscript(`Error: ${(e as Error).message}`);
    } finally {
      setBusy(false);
    }
  }

  const recActive = recording !== null;

  return (
    <ScrollView contentContainerStyle={styles.inner}>
      <View style={styles.row}>
        <LanguagePicker value={source} onChange={setSource} languages={languages} />
        <Text style={styles.arrow}>→</Text>
        <LanguagePicker value={target} onChange={setTarget} languages={languages} excludeAuto />
      </View>

      <Pressable
        onPress={toggle}
        disabled={busy}
        style={[
          styles.btn,
          recActive ? styles.btnRec : styles.btnIdle,
          busy && { opacity: 0.6 },
        ]}
      >
        {busy ? (
          <ActivityIndicator color="#fff" />
        ) : (
          <Text style={styles.btnText}>{recActive ? "■ Stop" : "● Record"}</Text>
        )}
      </Pressable>

      <View style={styles.output}>
        <Text style={styles.outputLabel}>Transcript</Text>
        <Text style={styles.outputText}>{transcript || "—"}</Text>
      </View>
      <View style={styles.output}>
        <Text style={styles.outputLabel}>Translated</Text>
        <Text style={styles.outputText}>{translated || "—"}</Text>
      </View>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  inner: { padding: 16, gap: 12, backgroundColor: "#f8fafc", flexGrow: 1 },
  row: { flexDirection: "row", alignItems: "center", gap: 8 },
  arrow: { fontSize: 18, color: "#64748b" },
  btn: { paddingVertical: 14, borderRadius: 10, alignItems: "center" },
  btnIdle: { backgroundColor: "#4f46e5" },
  btnRec: { backgroundColor: "#dc2626" },
  btnText: { color: "#fff", fontWeight: "700", fontSize: 16 },
  output: {
    borderWidth: 1,
    borderColor: "#e2e8f0",
    borderRadius: 10,
    backgroundColor: "#fff",
    padding: 12,
    minHeight: 80,
  },
  outputLabel: { fontSize: 12, color: "#64748b", fontWeight: "600", marginBottom: 6 },
  outputText: { fontSize: 15, color: "#0f172a" },
});
