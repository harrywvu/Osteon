"""Maintained MMSU program IDs shared through the account API.

Year options are deliberately broad until each current curriculum is confirmed.
"""
PROGRAMS = [
    ("bs_agribusiness", "BS Agribusiness", "Agriculture"),
    ("bs_agriculture", "BS Agriculture", "Agriculture"),
    ("bs_development_communication", "BS Development Communication", "Agriculture"),
    ("bs_environmental_science", "BS Environmental Science", "Agriculture"),
    ("bs_forestry", "BS Forestry", "Agriculture"),
    ("b_agricultural_technology", "Bachelor of Agricultural Technology", "Agriculture"),
    ("bs_food_technology", "BS Food Technology", "Agriculture"),
    ("bs_marine_biology", "BS Marine Biology", "Aquatic Sciences"),
    ("bs_fisheries", "BS Fisheries", "Aquatic Sciences"),
    ("bs_biology", "BS Biology", "Arts and Sciences"),
    ("bs_mathematics", "BS Mathematics", "Arts and Sciences"),
    ("bs_psychology", "BS Psychology", "Arts and Sciences"),
    ("ba_english_language", "BA English Language", "Arts and Sciences"),
    ("ba_communication", "BA Communication", "Arts and Sciences"),
    ("ba_sociology", "BA Sociology", "Arts and Sciences"),
    ("bs_computer_science", "BS Computer Science", "Computing and Information Sciences"),
    ("bs_information_technology", "BS Information Technology", "Computing and Information Sciences"),
    ("bs_accountancy", "BS Accountancy", "Business, Economics and Accountancy"),
    ("bs_accounting_technology", "BS Accounting Technology", "Business, Economics and Accountancy"),
    ("bs_business_administration", "BS Business Administration", "Business, Economics and Accountancy"),
    ("bs_cooperative_management", "BS Cooperative Management", "Business, Economics and Accountancy"),
    ("bs_economics", "BS Economics", "Business, Economics and Accountancy"),
    ("bs_entrepreneurship", "BS Entrepreneurship", "Business, Economics and Accountancy"),
    ("bs_hospitality_management", "BS Hospitality Management", "Business, Economics and Accountancy"),
    ("bs_tourism_management", "BS Tourism Management", "Business, Economics and Accountancy"),
    ("bs_agricultural_engineering", "BS Agricultural Engineering", "Engineering"),
    ("bs_civil_engineering", "BS Civil Engineering", "Engineering"),
    ("bs_computer_engineering", "BS Computer Engineering", "Engineering"),
    ("bs_electrical_engineering", "BS Electrical Engineering", "Engineering"),
    ("bs_mechanical_engineering", "BS Mechanical Engineering", "Engineering"),
    ("bs_electronics_engineering", "BS Electronics Engineering", "Engineering"),
    ("bs_ceramic_engineering", "BS Ceramic Engineering", "Engineering"),
    ("bs_chemical_engineering", "BS Chemical Engineering", "Engineering"),
    ("bs_nursing", "BS Nursing", "Health Sciences"),
    ("bs_pharmacy", "BS Pharmacy", "Health Sciences"),
    ("bs_physical_therapy", "BS Physical Therapy", "Health Sciences"),
    ("bs_radiologic_technology", "BS Radiologic Technology", "Health Sciences"),
    ("b_automotive_technology", "Bachelor in Automotive Technology", "Industrial Technology"),
    ("b_technical_teacher_education", "Bachelor in Technical Teacher Education", "Industrial Technology"),
    ("bs_industrial_technology", "BS Industrial Technology", "Industrial Technology"),
    ("b_technical_vocational_teacher_education", "Bachelor in Technical Vocational Teacher Education", "Teacher Education"),
    ("b_technology_livelihood_education", "Bachelor in Technology and Livelihood Education", "Teacher Education"),
    ("b_secondary_education", "Bachelor in Secondary Education", "Teacher Education"),
    ("b_elementary_education", "Bachelor in Elementary Education", "Teacher Education"),
    ("b_early_childhood_education", "Bachelor in Early Childhood Education", "Teacher Education"),
    ("b_special_needs_education", "Bachelor in Special Needs Education", "Teacher Education"),
    ("b_culture_arts_education", "Bachelor in Culture and the Arts Education", "Teacher Education"),
    ("b_physical_education", "Bachelor in Physical Education", "Teacher Education"),
    ("doctor_veterinary_medicine", "Doctor of Veterinary Medicine", "Veterinary Medicine"),
    ("doctor_dental_medicine", "Doctor of Dental Medicine", "Dentistry"),
    ("doctor_medicine", "Doctor of Medicine", "Medicine"),
    ("juris_doctor", "Juris Doctor", "Law"),
]

BY_ID = {program_id: {"id": program_id, "name": name, "college": college,
                      "year_options": list(range(1, 7))}
         for program_id, name, college in PROGRAMS}


def catalog():
    return list(BY_ID.values())
