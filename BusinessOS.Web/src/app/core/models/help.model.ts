export interface HelpFaqDto {
  id: string;
  category: string;
  question: string;
  answer: string;
}

export interface HelpDocSectionDto {
  title: string;
  description: string;
  topics: string[];
}

export interface HelpCenterDto {
  faqs: HelpFaqDto[];
  documentation: HelpDocSectionDto[];
}
