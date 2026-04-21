import * as ImagePicker from "expo-image-picker";
import * as FileSystem from "expo-file-system";
import { useState } from "react";
import {
  ActivityIndicator,
  Image,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from "react-native";
import { api } from "../src/api";
import { LanguagePicker } from "../src/LanguagePicker";
import { useLanguages } from "../src/LanguagesContext";

export default function OcrScreen() {
  const languages = useLanguages();
  const [target, setTarget] = useState("en");
  const [imageUri, setImageUri] = useState<string | null>(null);
  const [extracted, setExtracted] = useState("");
  const [translated, setTranslated] = useState("");
  const [busy, setBusy] = useState(false);

  async function pickImage(from: "camera" | "library") {
    const perm =
      from === "camera"
        ? await ImagePicker.requestCameraPermissionsAsync()
        : await ImagePicker.requestMediaLibraryPermissionsAsync();
    if (!perm.granted) return;
    const res =
      from === "camera"
        ? await ImagePicker.launchCameraAsync({ quality: 0.8 })
        : await ImagePicker.launchImageLibraryAsync({ quality: 0.8 });
    if (res.canceled || !res.assets?.[0]) return;
    const asset = res.assets[0];
    setImageUri(asset.uri);
    await submit(asset.uri, asset.mimeType ?? "image/jpeg");
  }

  async function submit(uri: string, mimeType: string) {
    setBusy(true);
    setExtracted("");
    setTranslated("");
    try {
      const b64 = await FileSystem.readAsStringAsync(uri, {
        encoding: FileSystem.EncodingType.Base64,
      });
      const blob = base64ToBlob(b64, mimeType);
      const r = await api.ocr(blob, target);
      setExtracted(r.extracted);
      setTranslated(r.translated);
    } catch (e) {
      setExtracted(`Error: ${(e as Error).message}`);
    } finally {
      setBusy(false);
    }
  }

  return (
    <ScrollView contentContainerStyle={styles.inner}>
      <View style={styles.row}>
        <Text style={styles.label}>To</Text>
        <LanguagePicker value={target} onChange={setTarget} languages={languages} excludeAuto />
      </View>

      <View style={styles.actions}>
        <Pressable style={styles.btn} onPress={() => pickImage("camera")}>
          <Text style={styles.btnText}>Camera</Text>
        </Pressable>
        <Pressable style={styles.btn} onPress={() => pickImage("library")}>
          <Text style={styles.btnText}>Gallery</Text>
        </Pressable>
      </View>

      {imageUri && <Image source={{ uri: imageUri }} style={styles.preview} resizeMode="contain" />}

      {busy && <ActivityIndicator style={{ marginVertical: 12 }} />}

      <View style={styles.output}>
        <Text style={styles.outputLabel}>Extracted</Text>
        <Text style={styles.outputText}>{extracted || "—"}</Text>
      </View>
      <View style={styles.output}>
        <Text style={styles.outputLabel}>Translated</Text>
        <Text style={styles.outputText}>{translated || "—"}</Text>
      </View>
    </ScrollView>
  );
}

function base64ToBlob(b64: string, mimeType: string): Blob {
  const binary = atob(b64);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  return new Blob([bytes], { type: mimeType });
}

const styles = StyleSheet.create({
  inner: { padding: 16, gap: 12, backgroundColor: "#f8fafc", flexGrow: 1 },
  row: { flexDirection: "row", alignItems: "center", gap: 8 },
  label: { color: "#64748b", fontSize: 14 },
  actions: { flexDirection: "row", gap: 10 },
  btn: {
    flex: 1,
    backgroundColor: "#4f46e5",
    paddingVertical: 12,
    borderRadius: 10,
    alignItems: "center",
  },
  btnText: { color: "#fff", fontWeight: "600" },
  preview: { width: "100%", height: 220, borderRadius: 10, backgroundColor: "#e2e8f0" },
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
