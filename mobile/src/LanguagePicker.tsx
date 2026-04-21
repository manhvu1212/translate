import { useState } from "react";
import { Modal, Pressable, ScrollView, StyleSheet, Text, View } from "react-native";
import type { Language } from "@translate/shared";

interface Props {
  value: string;
  onChange: (code: string) => void;
  languages: Language[];
  excludeAuto?: boolean;
}

export function LanguagePicker({ value, onChange, languages, excludeAuto }: Props) {
  const [open, setOpen] = useState(false);
  const list = excludeAuto ? languages.filter((l) => l.code !== "auto") : languages;
  const current = list.find((l) => l.code === value) ?? list[0];

  return (
    <>
      <Pressable style={styles.btn} onPress={() => setOpen(true)}>
        <Text style={styles.btnText}>{current?.nativeName ?? value}</Text>
      </Pressable>
      <Modal transparent visible={open} animationType="fade" onRequestClose={() => setOpen(false)}>
        <Pressable style={styles.backdrop} onPress={() => setOpen(false)}>
          <View style={styles.sheet}>
            <ScrollView>
              {list.map((l) => (
                <Pressable
                  key={l.code}
                  style={styles.row}
                  onPress={() => {
                    onChange(l.code);
                    setOpen(false);
                  }}
                >
                  <Text style={styles.rowText}>
                    {l.nativeName} <Text style={styles.rowSub}>({l.name})</Text>
                  </Text>
                </Pressable>
              ))}
            </ScrollView>
          </View>
        </Pressable>
      </Modal>
    </>
  );
}

const styles = StyleSheet.create({
  btn: {
    paddingHorizontal: 12,
    paddingVertical: 8,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: "#cbd5e1",
    backgroundColor: "#fff",
  },
  btnText: { fontSize: 14, color: "#0f172a" },
  backdrop: { flex: 1, backgroundColor: "rgba(0,0,0,0.4)", justifyContent: "flex-end" },
  sheet: { maxHeight: "70%", backgroundColor: "#fff", borderTopLeftRadius: 16, borderTopRightRadius: 16 },
  row: { paddingHorizontal: 16, paddingVertical: 14, borderBottomWidth: 1, borderBottomColor: "#f1f5f9" },
  rowText: { fontSize: 15, color: "#0f172a" },
  rowSub: { color: "#64748b", fontSize: 13 },
});
