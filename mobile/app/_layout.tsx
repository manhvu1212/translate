import { Tabs } from "expo-router";
import { StatusBar } from "expo-status-bar";
import { SafeAreaProvider } from "react-native-safe-area-context";
import { LanguagesProvider } from "../src/LanguagesContext";

export default function RootLayout() {
  return (
    <SafeAreaProvider>
      <LanguagesProvider>
        <StatusBar style="auto" />
        <Tabs screenOptions={{ headerShown: true }}>
          <Tabs.Screen name="index" options={{ title: "Text" }} />
          <Tabs.Screen name="ocr" options={{ title: "Image" }} />
          <Tabs.Screen name="voice" options={{ title: "Voice" }} />
        </Tabs>
      </LanguagesProvider>
    </SafeAreaProvider>
  );
}
