import Constants from "expo-constants";
import { TranslateClient } from "@translate/shared";

const apiUrl =
  (Constants.expoConfig?.extra as { apiUrl?: string } | undefined)?.apiUrl ??
  "http://localhost:8000";

export const api = new TranslateClient(apiUrl);
